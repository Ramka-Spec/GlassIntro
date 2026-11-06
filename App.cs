using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace GlassIntro
{
    internal static class Program
    {
        public static void Log(Exception e)
        {
            try
            {
                Directory.CreateDirectory(Settings.Folder);
                File.AppendAllText(System.IO.Path.Combine(Settings.Folder, "error.log"),
                    DateTime.Now + " " + e + Environment.NewLine + Environment.NewLine);
            }
            catch (IOException) { }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "GlassIntro.SingleInstance", out created))
            {
                if (!created)
                {
                    if (args.Length == 0 || args[0] != "--wait") return;
                    try
                    {
                        if (!mutex.WaitOne(15000)) return;
                    }
                    catch (AbandonedMutexException) { }
                }

                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log(e.ExceptionObject as Exception);
                System.Windows.Application app = new System.Windows.Application();
                app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                app.DispatcherUnhandledException += (s, e) =>
                {
                    Log(e.Exception);
                    e.Handled = true;
                };
                Shell shell = new Shell();
                app.Run();
                GC.KeepAlive(shell);
            }
        }
    }

    internal sealed class Shell
    {
        private readonly GlassEngine engine = new GlassEngine();
        private readonly MediaController media;
        private readonly ControlPanel panel;
        private readonly Island island;
        private readonly Forms.NotifyIcon tray = new Forms.NotifyIcon();
        private readonly DispatcherTimer watcher = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly IntPtr iconHandle;

        public Shell()
        {
            media = new MediaController(Dispatcher.CurrentDispatcher);
            panel = new ControlPanel(engine);
            island = new Island(media, engine);

            using (Bitmap image = DrawIcon())
            {
                iconHandle = image.GetHicon();
                tray.Icon = Icon.FromHandle(iconHandle);
            }
            tray.Text = "GlassIntro";
            tray.Visible = true;
            tray.MouseUp += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left || e.Button == Forms.MouseButtons.Right) panel.Toggle();
            };

            engine.Notify += text => tray.ShowBalloonTip(3000, "GlassIntro", text, Forms.ToolTipIcon.Info);
            engine.Exited += Exit;
            watcher.Tick += delegate { island.SetSuppressed(engine.IsForegroundFullscreen()); };
            watcher.Start();
            var ignored = media.StartAsync();
        }

        private void Exit()
        {
            watcher.Stop();
            tray.Visible = false;
            tray.Dispose();
            Native.DestroyIcon(iconHandle);
            System.Windows.Application.Current.Shutdown();
        }

        private static Bitmap DrawIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush fill = new LinearGradientBrush(new Rectangle(0, 0, 32, 32),
                    Color.FromArgb(240, 230, 245, 255), Color.FromArgb(200, 120, 170, 230), 60f))
                using (Pen edge = new Pen(Color.White, 1.6f))
                using (SolidBrush shine = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                {
                    g.FillEllipse(fill, 3, 3, 26, 26);
                    g.DrawEllipse(edge, 3, 3, 26, 26);
                    g.FillEllipse(shine, 9, 7, 8, 5);
                }
            }
            return bmp;
        }
    }
}
