using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlassIntro
{
    /// <summary>
    /// Создаёт стеклянные версии значков ярлыков и записывает их в свойства ярлыков.
    /// Прежние значки хранятся в backup.tsv и возвращаются при отключении.
    /// </summary>
    internal static class GlassIcons
    {
        private static readonly string Folder = System.IO.Path.Combine(Settings.Folder, "icons");
        private static readonly string BackupFile = System.IO.Path.Combine(Folder, "backup.tsv");
        private static readonly int[] Sizes = { 256, 64, 48, 32, 24, 16 };
        private const string Style = "v2";
        private static readonly string StyleStamp = System.IO.Path.Combine(Folder, Style + ".stamp");
        private static readonly object Gate = new object();

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx, cy; }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType, bmWidth, bmHeight, bmWidthBytes;
            public ushort bmPlanes, bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public ushort biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr bitmap);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);
        [DllImport("shell32.dll")] private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
        [DllImport("gdi32.dll")] private static extern int GetObject(IntPtr handle, int size, out BITMAP bitmap);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
        [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER info, uint usage);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        public static void ApplyAsync(Action<int> finished)
        {
            Thread thread = new Thread(() =>
            {
                try
                {
                    finished(Apply());
                }
                catch (Exception e)
                {
                    Program.Log(e);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        private static IEnumerable<string> Shortcuts()
        {
            string[] folders =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
            };
            foreach (string folder in folders)
            {
                foreach (string file in FilesIn(folder))
                {
                    string ext = System.IO.Path.GetExtension(file);
                    if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".url", StringComparison.OrdinalIgnoreCase)) yield return file;
                }
            }
        }

        private static List<string> FilesIn(string folder)
        {
            List<string> found = new List<string>();
            if (!Directory.Exists(folder)) return found;
            try
            {
                found.AddRange(Directory.GetFiles(folder));
                foreach (string child in Directory.GetDirectories(folder)) found.AddRange(FilesIn(child));
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            return found;
        }

        public static void PrepareAsync(string exe, Action<string> finished)
        {
            Thread thread = new Thread(() =>
            {
                string result = null;
                try
                {
                    lock (Gate)
                    {
                        Directory.CreateDirectory(Folder);
                        string ico = System.IO.Path.Combine(Folder, Hash(Style + "app" + exe) + ".ico");
                        if (!File.Exists(ico))
                        {
                            BitmapSource source = LoadShellImage(exe);
                            if (source != null) WriteIco(source, ico);
                        }
                        if (File.Exists(ico)) result = ico;
                    }
                }
                catch (Exception e)
                {
                    Program.Log(e);
                }
                finished(result);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        private static int Apply()
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                if (!File.Exists(StyleStamp))
                {
                    // Значки прошлого оформления пересоздаём из оригиналов.
                    Restore();
                    File.WriteAllText(StyleStamp, Style);
                }
                Dictionary<string, string> backup = LoadBackup();
                int count = 0;
                foreach (string file in Shortcuts())
                {
                    try
                    {
                        string current = ReadIcon(file);
                        if (current.IndexOf(Folder, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        string ico = System.IO.Path.Combine(Folder, Hash(Style + file) + ".ico");
                        BitmapSource source = LoadShellImage(file);
                        if (source == null) continue;
                        WriteIco(source, ico);
                        // Сначала сохраняем оригинал: сбой записи не должен его потерять.
                        backup[file] = current;
                        SaveBackup(backup);
                        WriteIcon(file, ico);
                        count++;
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (Exception e) when (e is COMException || e is IOException || e is ArgumentException)
                    {
                        Program.Log(e);
                    }
                }
                if (count > 0) Refresh();
                return count;
            }
        }

        public static void Restore()
        {
            lock (Gate)
            {
                Dictionary<string, string> backup = LoadBackup();
                List<string> restored = new List<string>();
                foreach (KeyValuePair<string, string> pair in backup)
                {
                    try
                    {
                        if (File.Exists(pair.Key)) WriteIcon(pair.Key, pair.Value, true);
                        restored.Add(pair.Key);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (Exception e) when (e is COMException || e is IOException)
                    {
                        Program.Log(e);
                    }
                }
                foreach (string file in restored) backup.Remove(file);
                SaveBackup(backup);
                Refresh();
            }
        }

        private static void Refresh()
        {
            SHChangeNotify(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero);
        }

        private static string Hash(string text)
        {
            using (MD5 md5 = MD5.Create())
            {
                return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(text.ToLowerInvariant()))).Replace("-", "");
            }
        }

        private static Dictionary<string, string> LoadBackup()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(BackupFile)) return map;
            foreach (string line in File.ReadAllLines(BackupFile, Encoding.UTF8))
            {
                string[] parts = line.Split('\t');
                if (parts.Length == 2) map[parts[0]] = parts[1];
            }
            return map;
        }

        private static void SaveBackup(Dictionary<string, string> map)
        {
            Directory.CreateDirectory(Folder);
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, string> pair in map) lines.Add(pair.Key + "\t" + pair.Value);
            File.WriteAllLines(BackupFile, lines, Encoding.UTF8);
        }

        private static bool IsUrl(string file)
        {
            return System.IO.Path.GetExtension(file).Equals(".url", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadIcon(string file)
        {
            if (IsUrl(file))
            {
                string icon = "", index = "0";
                foreach (string line in File.ReadAllLines(file, Encoding.Default))
                {
                    if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase)) icon = line.Substring(9);
                    if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase)) index = line.Substring(10);
                }
                return icon + "|" + index;
            }
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic link = shell.CreateShortcut(file);
            return (string)link.IconLocation;
        }

        private static void WriteIcon(string file, string value, bool original = false)
        {
            if (IsUrl(file))
            {
                string[] parts = (original ? value : value + "|0").Split('|');
                string icon = parts[0];
                string index = parts.Length > 1 ? parts[1] : "0";
                List<string> lines = new List<string>();
                bool hasIcon = false, hasIndex = false;
                foreach (string line in File.ReadAllLines(file, Encoding.Default))
                {
                    if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Add("IconFile=" + icon);
                        hasIcon = true;
                    }
                    else if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Add("IconIndex=" + index);
                        hasIndex = true;
                    }
                    else
                    {
                        lines.Add(line);
                    }
                }
                if (!hasIcon) lines.Add("IconFile=" + icon);
                if (!hasIndex) lines.Add("IconIndex=" + index);
                File.WriteAllLines(file, lines, Encoding.Default);
                return;
            }
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic link = shell.CreateShortcut(file);
            link.IconLocation = original ? value : value + ",0";
            link.Save();
        }

        private static BitmapSource LoadShellImage(string file)
        {
            Guid iid = typeof(IShellItemImageFactory).GUID;
            IShellItemImageFactory factory;
            if (SHCreateItemFromParsingName(file, IntPtr.Zero, ref iid, out factory) != 0 || factory == null) return null;
            SIZE size = new SIZE { cx = 256, cy = 256 };
            IntPtr handle;
            // Только значок и допускается масштаб: без миниатюр содержимого файла.
            if (factory.GetImage(size, 0x4 | 0x1, out handle) != 0) return null;
            try
            {
                BITMAP info;
                if (GetObject(handle, Marshal.SizeOf(typeof(BITMAP)), out info) == 0) return null;
                int w = info.bmWidth, h = Math.Abs(info.bmHeight);
                byte[] pixels = new byte[w * h * 4];
                BITMAPINFOHEADER header = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                    biWidth = w,
                    biHeight = -h,
                    biPlanes = 1,
                    biBitCount = 32
                };
                IntPtr dc = GetDC(IntPtr.Zero);
                try
                {
                    if (GetDIBits(dc, handle, 0, (uint)h, pixels, ref header, 0) == 0) return null;
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, dc);
                }
                bool hasAlpha = false;
                for (int i = 3; i < pixels.Length; i += 4)
                {
                    if (pixels[i] != 0) { hasAlpha = true; break; }
                }
                BitmapSource image = BitmapSource.Create(w, h, 96, 96,
                    hasAlpha ? PixelFormats.Pbgra32 : PixelFormats.Bgr32, null, pixels, w * 4);
                image.Freeze();
                return image;
            }
            finally
            {
                DeleteObject(handle);
            }
        }

        private static byte[] Render(BitmapSource icon, int size)
        {
            DrawingVisual visual = new DrawingVisual();
            double s = size;
            Rect tile = new Rect(s * 0.05, s * 0.05, s * 0.9, s * 0.9);
            double radius = s * 0.215;
            using (DrawingContext dc = visual.RenderOpen())
            {
                LinearGradientBrush body = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(195, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(130, 232, 240, 250), 0.55),
                    new GradientStop(Color.FromArgb(170, 245, 249, 255), 1)
                }, new Point(0, 0), new Point(0.5, 1));
                LinearGradientBrush edge = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(245, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(70, 255, 255, 255), 0.5),
                    new GradientStop(Color.FromArgb(170, 255, 255, 255), 1)
                }, new Point(0, 0), new Point(1, 1));
                dc.DrawRoundedRectangle(body, new Pen(edge, Math.Max(1, s * 0.012)), tile, radius, radius);

                double inner = s * 0.66;
                dc.DrawImage(icon, new Rect((s - inner) / 2, (s - inner) / 2, inner, inner));

                // Блик сверху, как на выпуклом стекле.
                dc.PushClip(new RectangleGeometry(tile, radius, radius));
                dc.DrawEllipse(new LinearGradientBrush(Color.FromArgb(95, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90),
                    null, new Point(s / 2, s * 0.06), s * 0.62, s * 0.34);
                dc.Pop();
            }
            RenderTargetBitmap bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            PngBitmapEncoder png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            using (MemoryStream memory = new MemoryStream())
            {
                png.Save(memory);
                return memory.ToArray();
            }
        }

        private static void WriteIco(BitmapSource icon, string path)
        {
            List<byte[]> images = new List<byte[]>();
            foreach (int size in Sizes) images.Add(Render(icon, size));
            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                w.Write((ushort)0);
                w.Write((ushort)1);
                w.Write((ushort)images.Count);
                int offset = 6 + 16 * images.Count;
                for (int i = 0; i < images.Count; i++)
                {
                    byte side = (byte)(Sizes[i] >= 256 ? 0 : Sizes[i]);
                    w.Write(side);
                    w.Write(side);
                    w.Write((byte)0);
                    w.Write((byte)0);
                    w.Write((ushort)1);
                    w.Write((ushort)32);
                    w.Write(images[i].Length);
                    w.Write(offset);
                    offset += images[i].Length;
                }
                foreach (byte[] image in images) w.Write(image);
            }
        }
    }
}
