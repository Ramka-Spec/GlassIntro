// GlassIntro: стеклянный стиль окон Windows. Системные файлы не изменяются, в чужие процессы код не внедряется.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GlassIntro
{
    internal static class Native
    {
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const long WS_CAPTION = 0x00C00000L;
        public const long WS_EX_LAYERED = 0x00080000L;
        public const long WS_EX_TOOLWINDOW = 0x00000080L;
        public const long WS_EX_APPWINDOW = 0x00040000L;
        public const long WS_THICKFRAME = 0x00040000L;
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const uint LWA_ALPHA = 2;
        public const uint GA_ROOT = 2;
        public const uint MONITOR_DEFAULTTONEAREST = 2;
        public const int DWMWA_CLOAKED = 14;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        public const uint EVENT_SYSTEM_FOREGROUND = 3;
        public const uint EVENT_OBJECT_SHOW = 0x8002;
        public const int WM_HOTKEY = 0x0312;
        public const int ACCENT_DISABLED = 0;
        public const int ACCENT_BLUR = 3;
        public const int ACCENT_ACRYLIC = 4;
        public const int WCA_ACCENT_POLICY = 19;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CompositionData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
        public delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")] public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref CompositionData data);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
        public static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref uint size);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

        public static void SetAccent(IntPtr hwnd, int state, uint color)
        {
            AccentPolicy policy = new AccentPolicy();
            policy.AccentState = state;
            policy.AccentFlags = 2;
            policy.GradientColor = color;
            int size = Marshal.SizeOf(typeof(AccentPolicy));
            IntPtr memory = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, memory, false);
                CompositionData data = new CompositionData();
                data.Attribute = WCA_ACCENT_POLICY;
                data.Data = memory;
                data.SizeOfData = size;
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(memory);
            }
        }

        public static string ClassOf(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }

    internal enum Effect { Acrylic = 0, Blur = 1, Mica = 2 }

    internal class Settings
    {
        public bool Enabled = true;
        public bool Taskbar = true;
        public Effect Mode;
        public int Percent = 15;
        public int SavedTransparency = -1;

        public static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlassIntro");
        private static readonly string FilePath = Path.Combine(Folder, "settings.ini");

        public static Settings Load(Effect defaultMode)
        {
            Settings s = new Settings();
            s.Mode = defaultMode;
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] kv = line.Split(new char[] { '=' }, 2);
                    if (kv.Length != 2) continue;
                    int value;
                    if (!int.TryParse(kv[1].Trim(), out value)) continue;
                    switch (kv[0].Trim())
                    {
                        case "enabled": s.Enabled = value != 0; break;
                        case "taskbar": s.Taskbar = value != 0; break;
                        case "mode": if (value >= 0 && value <= 2) s.Mode = (Effect)value; break;
                        case "percent": if (value >= 5 && value <= 40) s.Percent = value; break;
                        case "savedTransparency": s.SavedTransparency = value; break;
                    }
                }
            }
            catch (IOException) { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllLines(FilePath, new string[] {
                    "enabled=" + (Enabled ? 1 : 0),
                    "taskbar=" + (Taskbar ? 1 : 0),
                    "mode=" + (int)Mode,
                    "percent=" + Percent,
                    "savedTransparency=" + SavedTransparency
                });
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal class HotkeyWindow : NativeWindow
    {
        private readonly Action pressed;

        public HotkeyWindow(Action pressed)
        {
            this.pressed = pressed;
            CreateHandle(new CreateParams());
            // Ctrl + Alt + G, без автоповтора
            Native.RegisterHotKey(Handle, 1, 0x0002 | 0x0001 | 0x4000, (uint)Keys.G);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY) pressed();
            base.WndProc(ref m);
        }

        public void Release()
        {
            Native.UnregisterHotKey(Handle, 1);
            DestroyHandle();
        }
    }

    internal class GlassApp : ApplicationContext
    {
        private const uint GlassTint = 0x30201A1A;
        private const string MenuClass = "#32768";
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private static readonly string[] DefaultExcluded = {
            "# Программы из этого списка GlassIntro не трогает. Одна программа на строку, без .exe",
            "cs2", "csgo", "javaw", "java", "Minecraft.Windows", "RobloxPlayerBeta",
            "dnplayer", "VirtualBoxVM", "mstsc", "obs64",
            "SearchHost", "SearchApp", "StartMenuExperienceHost", "ShellExperienceHost",
            "TextInputHost", "LockApp"
        };
        private static readonly HashSet<string> SkippedClasses = new HashSet<string>(StringComparer.Ordinal) {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
            "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow"
        };

        private readonly int build;
        private readonly Settings settings;
        private readonly Dictionary<IntPtr, long> applied = new Dictionary<IntPtr, long>();
        private readonly HashSet<IntPtr> layeredTaskbars = new HashSet<IntPtr>();
        private readonly Dictionary<uint, string> processNames = new Dictionary<uint, string>();
        private readonly HashSet<string> excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string excludeFile = Path.Combine(Settings.Folder, "exclude.txt");
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly Native.WinEventProc hookProc;
        private readonly HotkeyWindow hotkey;
        private readonly Icon trayImage;
        private IntPtr iconHandle = IntPtr.Zero;
        private IntPtr hookForeground;
        private IntPtr hookShow;
        private IntPtr lastGlassIntrodow = IntPtr.Zero;
        private DateTime excludeStamp = DateTime.MinValue;
        private ToolStripMenuItem enabledItem;
        private ToolStripMenuItem taskbarItem;
        private ToolStripMenuItem autostartItem;
        private readonly List<ToolStripMenuItem> modeItems = new List<ToolStripMenuItem>();
        private readonly List<ToolStripMenuItem> percentItems = new List<ToolStripMenuItem>();
        private int ticks;

        public GlassApp()
        {
            build = ReadBuild();
            settings = Settings.Load(build >= 22000 ? Effect.Acrylic : Effect.Blur);
            if (settings.Mode == Effect.Mica && build < 22621) settings.Mode = Effect.Acrylic;
            LoadExcluded();
            if (settings.Enabled) EnableSystemTransparency();

            trayImage = MakeIcon();
            tray.Icon = trayImage;
            tray.Text = "GlassIntro";
            tray.ContextMenuStrip = BuildMenu();
            tray.Visible = true;

            hookProc = OnWinEvent;
            hookForeground = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, hookProc, 0, 0, 0);
            hookShow = Native.SetWinEventHook(Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_SHOW,
                IntPtr.Zero, hookProc, 0, 0, 0);
            hotkey = new HotkeyWindow(ToggleEnabled);

            timer.Interval = 1500;
            timer.Tick += delegate { Scan(); };
            timer.Start();
            Scan();
        }

        private static int ReadBuild()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    int value;
                    if (key != null && int.TryParse(Convert.ToString(key.GetValue("CurrentBuildNumber")), out value)) return value;
                }
            }
            catch (System.Security.SecurityException) { }
            return 0;
        }

        private Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush fill = new LinearGradientBrush(new Rectangle(0, 0, 32, 32),
                    Color.FromArgb(235, 170, 225, 255), Color.FromArgb(235, 40, 110, 220), 45f))
                using (Pen edge = new Pen(Color.White, 2f))
                {
                    g.FillEllipse(fill, 3, 3, 26, 26);
                    g.DrawEllipse(edge, 3, 3, 26, 26);
                }
                iconHandle = bmp.GetHicon();
                return Icon.FromHandle(iconHandle);
            }
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            enabledItem = new ToolStripMenuItem("Стекло включено (Ctrl+Alt+G)");
            enabledItem.Click += delegate { ToggleEnabled(); };
            menu.Items.Add(enabledItem);
            menu.Items.Add(new ToolStripSeparator());

            AddMode(menu, "Акрил: матовое стекло", Effect.Acrylic, true);
            AddMode(menu, "Размытие: быстрее на Windows 10", Effect.Blur, true);
            AddMode(menu, "Только заголовки окон (Windows 11)", Effect.Mica, build >= 22621);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem strength = new ToolStripMenuItem("Прозрачность");
            foreach (int percent in new int[] { 5, 10, 15, 20, 30 })
            {
                int current = percent;
                ToolStripMenuItem item = new ToolStripMenuItem(percent + "%");
                item.Tag = percent;
                item.Click += delegate
                {
                    settings.Percent = current;
                    Reapply();
                };
                percentItems.Add(item);
                strength.DropDownItems.Add(item);
            }
            menu.Items.Add(strength);

            taskbarItem = new ToolStripMenuItem("Стеклянная панель задач");
            taskbarItem.Click += delegate
            {
                settings.Taskbar = !settings.Taskbar;
                if (!settings.Taskbar) ClearTaskbar();
                Reapply();
            };
            menu.Items.Add(taskbarItem);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exclude = new ToolStripMenuItem("Не трогать последнее окно");
            exclude.Click += delegate { ExcludeLastWindow(); };
            menu.Items.Add(exclude);
            ToolStripMenuItem list = new ToolStripMenuItem("Список исключений…");
            list.Click += delegate { OpenExcludeList(); };
            menu.Items.Add(list);

            autostartItem = new ToolStripMenuItem("Запускать вместе с Windows");
            autostartItem.Click += delegate { SetAutostart(!IsAutostart()); UpdateMenu(); };
            menu.Items.Add(autostartItem);
            menu.Items.Add(new ToolStripSeparator());

            if (!IsAdministrator())
            {
                ToolStripMenuItem admin = new ToolStripMenuItem("Перезапустить от администратора");
                admin.Click += delegate { RestartAsAdmin(); };
                menu.Items.Add(admin);
            }
            ToolStripMenuItem exit = new ToolStripMenuItem("Выход (вернуть как было)");
            exit.Click += delegate { Quit(); };
            menu.Items.Add(exit);
            UpdateMenu();
            return menu;
        }

        private static bool IsAdministrator()
        {
            using (System.Security.Principal.WindowsIdentity id = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                return new System.Security.Principal.WindowsPrincipal(id)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }

        private void RestartAsAdmin()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(Application.ExecutablePath, "--wait");
                info.UseShellExecute = true;
                info.Verb = "runas";
                System.Diagnostics.Process.Start(info);
                Quit();
            }
            catch (System.ComponentModel.Win32Exception) { }
        }

        private void AddMode(ContextMenuStrip menu, string title, Effect mode, bool available)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(title);
            item.Tag = mode;
            item.Enabled = available;
            item.Click += delegate
            {
                settings.Mode = mode;
                Reapply();
            };
            modeItems.Add(item);
            menu.Items.Add(item);
        }

        private void UpdateMenu()
        {
            enabledItem.Checked = settings.Enabled;
            taskbarItem.Checked = settings.Taskbar;
            autostartItem.Checked = IsAutostart();
            foreach (ToolStripMenuItem item in modeItems) item.Checked = (Effect)item.Tag == settings.Mode;
            foreach (ToolStripMenuItem item in percentItems) item.Checked = (int)item.Tag == settings.Percent;
        }

        private void ToggleEnabled()
        {
            settings.Enabled = !settings.Enabled;
            if (settings.Enabled)
            {
                EnableSystemTransparency();
            }
            else
            {
                ClearTaskbar();
                RestoreSystemTransparency();
            }
            Reapply();
        }

        private void Reapply()
        {
            RestoreAll();
            settings.Save();
            UpdateMenu();
            Scan();
        }

        private void Scan()
        {
            ReloadExcludedIfChanged();
            if (++ticks % 40 == 0) processNames.Clear();

            List<IntPtr> dead = new List<IntPtr>();
            foreach (IntPtr h in applied.Keys) if (!Native.IsWindow(h)) dead.Add(h);
            foreach (IntPtr h in dead) applied.Remove(h);

            if (!settings.Enabled) return;
            List<IntPtr> windows = new List<IntPtr>();
            Native.EnumWindows(delegate(IntPtr h, IntPtr p) { windows.Add(h); return true; }, IntPtr.Zero);
            foreach (IntPtr h in windows)
            {
                Process(h);
                if (settings.Taskbar)
                {
                    string cls = Native.ClassOf(h);
                    if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") ApplyTaskbar(h);
                }
            }
        }

        private void ApplyTaskbar(IntPtr h)
        {
            Native.SetAccent(h, TaskbarAccent(), GlassTint);
            if (build < 22000) return;
            // На Windows 11 панель рисует свой фон поверх размытия, поэтому делаем её полупрозрачной целиком.
            long ex = Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE).ToInt64();
            if ((ex & Native.WS_EX_LAYERED) != 0 && !layeredTaskbars.Contains(h)) return;
            if ((ex & Native.WS_EX_LAYERED) == 0)
            {
                Native.SetWindowLongPtr(h, Native.GWL_EXSTYLE, new IntPtr(ex | Native.WS_EX_LAYERED));
                layeredTaskbars.Add(h);
            }
            int percent = Math.Min(40, settings.Percent * 2);
            Native.SetLayeredWindowAttributes(h, 0, (byte)(255 * (100 - percent) / 100), Native.LWA_ALPHA);
        }

        private int TaskbarAccent()
        {
            return settings.Mode == Effect.Blur ? Native.ACCENT_BLUR : Native.ACCENT_ACRYLIC;
        }

        private void OnWinEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (hwnd == IntPtr.Zero || idObject != 0 || idChild != 0) return;
            if (settings.Enabled) Process(hwnd);
            if (evt == Native.EVENT_SYSTEM_FOREGROUND && applied.ContainsKey(hwnd)) lastGlassIntrodow = hwnd;
        }

        private void Process(IntPtr hwnd)
        {
            if (!IsEligible(hwnd))
            {
                if (applied.ContainsKey(hwnd)) Restore(hwnd);
                return;
            }
            if (applied.ContainsKey(hwnd)) return;

            long exStyle = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
            if (settings.Mode == Effect.Mica)
            {
                if (Native.ClassOf(hwnd) == MenuClass) return;
                int backdrop = 3;
                Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
            else
            {
                // Окна, которые рисуют прозрачность сами, трогать нельзя: они перестанут отображаться.
                if ((exStyle & Native.WS_EX_LAYERED) != 0) return;
                Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(exStyle | Native.WS_EX_LAYERED));
                Native.SetLayeredWindowAttributes(hwnd, 0, (byte)(255 * (100 - settings.Percent) / 100), Native.LWA_ALPHA);
                Native.SetAccent(hwnd, settings.Mode == Effect.Blur ? Native.ACCENT_BLUR : Native.ACCENT_ACRYLIC, GlassTint);
            }
            applied[hwnd] = exStyle;
        }

        private bool IsEligible(IntPtr hwnd)
        {
            if (!Native.IsWindow(hwnd) || !Native.IsWindowVisible(hwnd)) return false;
            if (Native.GetAncestor(hwnd, Native.GA_ROOT) != hwnd) return false;

            long style = Native.GetWindowLongPtr(hwnd, Native.GWL_STYLE).ToInt64();
            long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
            string cls = Native.ClassOf(hwnd);
            if (SkippedClasses.Contains(cls)) return false;
            if (cls != MenuClass)
            {
                bool hasCaption = (style & Native.WS_CAPTION) == Native.WS_CAPTION;
                bool resizable = (style & Native.WS_THICKFRAME) != 0;
                bool onTaskbar = (ex & Native.WS_EX_APPWINDOW) != 0;
                if (!hasCaption && !resizable && !onTaskbar) return false;
                if ((ex & Native.WS_EX_TOOLWINDOW) != 0) return false;
            }

            int cloaked;
            if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0 && cloaked != 0) return false;
            if (IsFullscreen(hwnd)) return false;

            string name = ProcessNameOf(hwnd);
            return name.Length > 0 && !excluded.Contains(name)
                && !string.Equals(name, "GlassIntro", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFullscreen(IntPtr hwnd)
        {
            Native.RECT r;
            if (!Native.GetWindowRect(hwnd, out r)) return false;
            Native.MONITORINFO info = new Native.MONITORINFO();
            info.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO));
            if (!Native.GetMonitorInfo(Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST), ref info)) return false;
            return r.Left == info.rcMonitor.Left && r.Top == info.rcMonitor.Top
                && r.Right == info.rcMonitor.Right && r.Bottom == info.rcMonitor.Bottom;
        }

        private string ProcessNameOf(IntPtr hwnd)
        {
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            string name;
            if (processNames.TryGetValue(pid, out name)) return name;
            try
            {
                using (System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById((int)pid)) name = p.ProcessName;
            }
            catch (ArgumentException) { name = ""; }
            catch (InvalidOperationException) { name = ""; }
            if (IsGamePath(pid)) name = "";
            processNames[pid] = name;
            return name;
        }

        /// <summary>Игры из Steam, Epic и Riot не меняются, даже если запущены в окне.</summary>
        private static bool IsGamePath(uint pid)
        {
            IntPtr process = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (process == IntPtr.Zero) return false;
            try
            {
                StringBuilder path = new StringBuilder(1024);
                uint size = (uint)path.Capacity;
                if (!Native.QueryFullProcessImageName(process, 0, path, ref size)) return false;
                string full = path.ToString();
                foreach (string marker in new string[] { @"\steamapps\", @"\Epic Games\", @"\Riot Games\" })
                {
                    if (full.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
                return false;
            }
            finally
            {
                Native.CloseHandle(process);
            }
        }

        private void Restore(IntPtr hwnd)
        {
            long original;
            if (!applied.TryGetValue(hwnd, out original)) return;
            applied.Remove(hwnd);
            if (!Native.IsWindow(hwnd)) return;
            int auto = 0;
            Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_SYSTEMBACKDROP_TYPE, ref auto, sizeof(int));
            Native.SetAccent(hwnd, Native.ACCENT_DISABLED, 0);
            if ((original & Native.WS_EX_LAYERED) == 0)
            {
                Native.SetLayeredWindowAttributes(hwnd, 0, 255, Native.LWA_ALPHA);
                Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(original));
            }
        }

        private void RestoreAll()
        {
            foreach (IntPtr h in new List<IntPtr>(applied.Keys)) Restore(h);
        }

        private void ClearTaskbar()
        {
            List<IntPtr> bars = new List<IntPtr>();
            Native.EnumWindows(delegate(IntPtr h, IntPtr p)
            {
                string cls = Native.ClassOf(h);
                if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") bars.Add(h);
                return true;
            }, IntPtr.Zero);
            foreach (IntPtr h in bars)
            {
                Native.SetAccent(h, Native.ACCENT_DISABLED, 0);
                if (layeredTaskbars.Remove(h))
                {
                    long ex = Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE).ToInt64();
                    Native.SetLayeredWindowAttributes(h, 0, 255, Native.LWA_ALPHA);
                    Native.SetWindowLongPtr(h, Native.GWL_EXSTYLE, new IntPtr(ex & ~Native.WS_EX_LAYERED));
                }
            }
        }

        private void EnableSystemTransparency()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, true))
            {
                if (key == null) return;
                object value = key.GetValue("EnableTransparency");
                if (value is int && (int)value == 0)
                {
                    settings.SavedTransparency = 0;
                    key.SetValue("EnableTransparency", 1, RegistryValueKind.DWord);
                    settings.Save();
                }
            }
        }

        private void RestoreSystemTransparency()
        {
            if (settings.SavedTransparency < 0) return;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, true))
            {
                if (key != null) key.SetValue("EnableTransparency", settings.SavedTransparency, RegistryValueKind.DWord);
            }
            settings.SavedTransparency = -1;
            settings.Save();
        }

        private void LoadExcluded()
        {
            try
            {
                Directory.CreateDirectory(Settings.Folder);
                if (!File.Exists(excludeFile)) File.WriteAllLines(excludeFile, DefaultExcluded, Encoding.UTF8);
                excluded.Clear();
                foreach (string raw in File.ReadAllLines(excludeFile))
                {
                    string line = raw.Trim();
                    if (line.Length > 0 && line[0] != '#') excluded.Add(line);
                }
                excludeStamp = File.GetLastWriteTimeUtc(excludeFile);
            }
            catch (IOException)
            {
                foreach (string line in DefaultExcluded) if (line[0] != '#') excluded.Add(line);
            }
        }

        private void ReloadExcludedIfChanged()
        {
            try
            {
                if (File.GetLastWriteTimeUtc(excludeFile) != excludeStamp) LoadExcluded();
            }
            catch (IOException) { }
        }

        private void ExcludeLastWindow()
        {
            if (lastGlassIntrodow == IntPtr.Zero || !Native.IsWindow(lastGlassIntrodow))
            {
                tray.ShowBalloonTip(3000, "GlassIntro", "Сначала щёлкни по окну, которое не нужно менять", ToolTipIcon.Info);
                return;
            }
            string name = ProcessNameOf(lastGlassIntrodow);
            if (name.Length == 0) return;
            try
            {
                File.AppendAllLines(excludeFile, new string[] { name }, Encoding.UTF8);
            }
            catch (IOException) { return; }
            LoadExcluded();
            Reapply();
            tray.ShowBalloonTip(3000, "GlassIntro", "Больше не меняю: " + name, ToolTipIcon.Info);
        }

        private void OpenExcludeList()
        {
            try
            {
                System.Diagnostics.Process.Start("notepad.exe", "\"" + excludeFile + "\"");
            }
            catch (System.ComponentModel.Win32Exception) { }
        }

        private static bool IsAutostart()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                return key != null && key.GetValue("GlassIntro") != null;
            }
        }

        private static void SetAutostart(bool on)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null) return;
                if (on) key.SetValue("GlassIntro", "\"" + Application.ExecutablePath + "\"");
                else key.DeleteValue("GlassIntro", false);
            }
        }

        private void Quit()
        {
            timer.Stop();
            if (hookForeground != IntPtr.Zero) Native.UnhookWinEvent(hookForeground);
            if (hookShow != IntPtr.Zero) Native.UnhookWinEvent(hookShow);
            hotkey.Release();
            RestoreAll();
            ClearTaskbar();
            RestoreSystemTransparency();
            tray.Visible = false;
            tray.Dispose();
            trayImage.Dispose();
            if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
            ExitThread();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool created;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "GlassIntro.SingleInstance", out created))
            {
                if (!created)
                {
                    if (args.Length == 0 || args[0] != "--wait") return;
                    try
                    {
                        if (!mutex.WaitOne(15000)) return;
                    }
                    catch (System.Threading.AbandonedMutexException) { }
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new GlassApp());
            }
        }
    }
}
