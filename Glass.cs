using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace GlassIntro
{
    internal sealed class Spring
    {
        public double Value;
        public double Target;
        private double velocity;
        private const double Stiffness = 300;
        private const double Damping = 30;

        public Spring(double value)
        {
            Value = value;
            Target = value;
        }

        public void Snap(double value)
        {
            Value = value;
            Target = value;
            velocity = 0;
        }

        public bool Step(double dt)
        {
            double acceleration = Stiffness * (Target - Value) - Damping * velocity;
            velocity += acceleration * dt;
            Value += velocity * dt;
            if (Math.Abs(Target - Value) < 0.04 && Math.Abs(velocity) < 0.4)
            {
                Value = Target;
                velocity = 0;
                return true;
            }
            return false;
        }
    }

    internal static class Ui
    {
        public static readonly FontFamily Font = new FontFamily("Segoe UI Variable Text, Segoe UI");
        public static readonly CubicEase EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

        public static Color White(byte alpha)
        {
            return Color.FromArgb(alpha, 255, 255, 255);
        }

        public static SolidColorBrush Brush(byte alpha)
        {
            return new SolidColorBrush(White(alpha));
        }

        public static TextBlock Text(string text, double size, byte alpha = 255, FontWeight? weight = null)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = Font,
                FontSize = size,
                Foreground = Brush(alpha),
                FontWeight = weight ?? FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static System.Windows.Shapes.Path Icon(string data, double size, Brush fill)
        {
            return new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(data),
                Fill = fill,
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = false
            };
        }

        public static void Fade(UIElement element, double to, int ms = 180)
        {
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = EaseOut });
        }

        /// <summary>Нажатие с пружинным сжатием. Срабатывает при отпускании над элементом.</summary>
        public static void Pressable(FrameworkElement e, Action click, double pressed = 0.93)
        {
            ScaleTransform scale = new ScaleTransform(1, 1);
            e.RenderTransform = scale;
            e.RenderTransformOrigin = new Point(0.5, 0.5);
            e.Cursor = Cursors.Hand;
            Action<double, IEasingFunction, int> go = (to, ease, ms) =>
            {
                DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            };
            IEasingFunction back = new BackEase { Amplitude = 0.7, EasingMode = EasingMode.EaseOut };
            e.MouseLeftButtonDown += (s, a) =>
            {
                e.CaptureMouse();
                go(pressed, EaseOut, 90);
                a.Handled = true;
            };
            e.MouseLeftButtonUp += (s, a) =>
            {
                bool inside = e.IsMouseCaptured && e.IsMouseOver;
                e.ReleaseMouseCapture();
                go(1, back, 280);
                if (inside) click();
                a.Handled = true;
            };
            e.LostMouseCapture += (s, a) => go(1, back, 280);
        }
    }

    internal static class Icons
    {
        public const string Play = "M 8 5.5 V 18.5 C 8 19.3 8.9 19.8 9.5 19.4 L 20.5 12.9 C 21.2 12.5 21.2 11.5 20.5 11.1 L 9.5 4.6 C 8.9 4.2 8 4.7 8 5.5 Z";
        public const string Pause = "M 7 5 H 9.5 C 10.3 5 11 5.7 11 6.5 V 17.5 C 11 18.3 10.3 19 9.5 19 H 7 C 6.2 19 5.5 18.3 5.5 17.5 V 6.5 C 5.5 5.7 6.2 5 7 5 Z M 15 5 H 17.5 C 18.3 5 19 5.7 19 6.5 V 17.5 C 19 18.3 18.3 19 17.5 19 H 15 C 14.2 19 13.5 18.3 13.5 17.5 V 6.5 C 13.5 5.7 14.2 5 15 5 Z";
        public const string Next = "M 4 6 V 18 C 4 18.8 4.9 19.3 5.5 18.8 L 13 13.2 V 18 C 13 18.8 13.9 19.3 14.5 18.8 L 22 12.8 C 22.5 12.4 22.5 11.6 22 11.2 L 14.5 5.2 C 13.9 4.7 13 5.2 13 6 V 10.8 L 5.5 5.2 C 4.9 4.7 4 5.2 4 6 Z";
        public const string Previous = "M 20 6 V 18 C 20 18.8 19.1 19.3 18.5 18.8 L 11 13.2 V 18 C 11 18.8 10.1 19.3 9.5 18.8 L 2 12.8 C 1.5 12.4 1.5 11.6 2 11.2 L 9.5 5.2 C 10.1 4.7 11 5.2 11 6 V 10.8 L 18.5 5.2 C 19.1 4.7 20 5.2 20 6 Z";
        public const string Sparkle = "M 12 2 L 14.4 8.6 L 21 11 L 14.4 13.4 L 12 20 L 9.6 13.4 L 3 11 L 9.6 8.6 Z";
        public const string Music = "M 21 3 L 9 5.4 V 15.3 A 3.2 3.2 0 1 0 11 18.2 V 9.3 L 19 7.7 V 13.3 A 3.2 3.2 0 1 0 21 16.2 Z";
        public const string Taskbar = "M 3 17 H 21 V 21 H 3 Z M 6 3 H 18 C 19.7 3 21 4.3 21 6 V 15 H 3 V 6 C 3 4.3 4.3 3 6 3 Z";
        public const string Windows = "M 3 8 H 16 V 21 H 3 Z M 8 3 H 21 V 16 H 18 V 6 H 8 Z";
        public const string Volume = "M 3 9 V 15 H 7 L 13 20 V 4 L 7 9 Z M 16 8 A 5.5 5.5 0 0 1 16 16 L 14.6 14.6 A 3.5 3.5 0 0 0 14.6 9.4 Z";
    }

    /// <summary>Стеклянная поверхность: размытие Windows, блик и пружинное изменение формы.</summary>
    internal class GlassWindow : Window
    {
        protected readonly Grid Host = new Grid();
        protected readonly Border Surface = new Border();
        protected IntPtr Handle;
        private readonly Spring width;
        private readonly Spring height;
        private readonly Spring radius;
        private readonly Spring opacity;
        private readonly Stopwatch watch = new Stopwatch();
        private readonly RectangleGeometry clip = new RectangleGeometry();
        private readonly bool activatable;
        private readonly Brush glassEdge;
        private readonly Brush glassFill;
        private readonly Rectangle sheen;
        private bool blurred = true;
        private bool rendering;
        private bool hideWhenDone;
        private double lastTime;

        protected GlassWindow(double windowWidth, double windowHeight, HorizontalAlignment h, VerticalAlignment v, bool activatable)
        {
            this.activatable = activatable;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Width = windowWidth;
            Height = windowHeight;
            ShowActivated = activatable;
            FontFamily = Ui.Font;

            width = new Spring(100);
            height = new Spring(40);
            radius = new Spring(20);
            opacity = new Spring(0);

            Surface.HorizontalAlignment = h;
            Surface.VerticalAlignment = v;
            Surface.BorderThickness = new Thickness(1);
            Surface.BorderBrush = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Ui.White(220), 0),
                new GradientStop(Ui.White(45), 0.45),
                new GradientStop(Ui.White(25), 0.6),
                new GradientStop(Ui.White(130), 1)
            }, new Point(0, 0), new Point(1, 1));
            glassEdge = Surface.BorderBrush;
            glassFill = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(58, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb(18, 255, 255, 255), 0.5),
                new GradientStop(Color.FromArgb(34, 255, 255, 255), 1)
            }, new Point(0, 0), new Point(0.4, 1));
            Surface.Background = glassFill;
            Host.Clip = clip;

            sheen = new Rectangle
            {
                Height = 46,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false,
                Fill = new LinearGradientBrush(Ui.White(42), Ui.White(0), 90)
            };
            Grid layers = new Grid();
            layers.Children.Add(sheen);
            layers.Children.Add(Host);
            Surface.Child = layers;
            Grid root = new Grid { Background = Brushes.Transparent };
            root.Children.Add(Surface);
            Content = root;
            Apply();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Handle = new WindowInteropHelper(this).Handle;
            long ex = Native.GetWindowLongPtr(Handle, Native.GWL_EXSTYLE).ToInt64() | Native.WS_EX_TOOLWINDOW;
            if (!activatable) ex |= Native.WS_EX_NOACTIVATE;
            Native.SetWindowLongPtr(Handle, Native.GWL_EXSTYLE, new IntPtr(ex));
            if (blurred) Native.SetAccent(Handle, Native.ACCENT_ACRYLIC, 0x38141414);
            UpdateRegion();
        }

        /// <summary>true: стекло с размытием. false: сплошная тёмная поверхность со сглаженными краями.</summary>
        protected void SetGlass(bool on)
        {
            if (blurred == on) return;
            blurred = on;
            Surface.Background = on ? glassFill : new SolidColorBrush(Color.FromRgb(10, 10, 12));
            Surface.BorderBrush = on ? glassEdge : Ui.Brush(28);
            sheen.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            if (Handle == IntPtr.Zero) return;
            Native.SetAccent(Handle, on ? Native.ACCENT_ACRYLIC : Native.ACCENT_DISABLED, 0x38141414);
            UpdateRegion();
        }

        protected void Snap(double w, double h, double r, double o)
        {
            width.Snap(w);
            height.Snap(h);
            radius.Snap(r);
            opacity.Snap(o);
            Apply();
        }

        protected void AnimateTo(double w, double h, double r, double o, bool hideAfter = false)
        {
            width.Target = w;
            height.Target = h;
            radius.Target = r;
            opacity.Target = o;
            hideWhenDone = hideAfter;
            if (rendering) return;
            rendering = true;
            lastTime = 0;
            watch.Restart();
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            double now = watch.Elapsed.TotalSeconds;
            double dt = Math.Min(0.034, now - lastTime);
            lastTime = now;
            if (dt <= 0) return;
            bool done = true;
            for (int i = 0; i < 3; i++)
            {
                double step = dt / 3;
                done &= width.Step(step);
                done &= height.Step(step);
                done &= radius.Step(step);
                done &= opacity.Step(step);
            }
            Apply();
            if (done)
            {
                rendering = false;
                CompositionTarget.Rendering -= OnRendering;
                if (hideWhenDone) Hide();
            }
        }

        private void Apply()
        {
            Surface.Width = Math.Max(1, width.Value);
            Surface.Height = Math.Max(1, height.Value);
            Surface.CornerRadius = new CornerRadius(Math.Max(0, radius.Value));
            Surface.Opacity = Math.Max(0, Math.Min(1, opacity.Value));
            clip.Rect = new Rect(0, 0, Surface.Width, Surface.Height);
            clip.RadiusX = radius.Value;
            clip.RadiusY = radius.Value;
            UpdateRegion();
        }

        private void UpdateRegion()
        {
            if (Handle == IntPtr.Zero) return;
            if (!blurred)
            {
                // Без размытия форму рисует сам WPF: края получаются сглаженными.
                Native.SetWindowRgn(Handle, IntPtr.Zero, true);
                return;
            }
            PresentationSource source = PresentationSource.FromVisual(this);
            double scale = source != null && source.CompositionTarget != null
                ? source.CompositionTarget.TransformToDevice.M11 : 1;
            double x = 0, y = 0;
            if (Surface.HorizontalAlignment == HorizontalAlignment.Center) x = (Width - width.Value) / 2;
            else if (Surface.HorizontalAlignment == HorizontalAlignment.Right) x = Width - width.Value;
            if (Surface.VerticalAlignment == VerticalAlignment.Bottom) y = Height - height.Value;
            double diameter = radius.Value * 2 * scale;
            IntPtr region = Native.CreateRoundRectRgn(
                (int)Math.Round(x * scale), (int)Math.Round(y * scale),
                (int)Math.Round((x + width.Value) * scale) + 1, (int)Math.Round((y + height.Value) * scale) + 1,
                (int)diameter, (int)diameter);
            Native.SetWindowRgn(Handle, region, true);
        }
    }

    /// <summary>Переключатель-плитка, как в центре управления.</summary>
    internal sealed class Tile : Border
    {
        private readonly SolidColorBrush fill = Ui.Brush(28);
        private readonly SolidColorBrush ink = Ui.Brush(255);
        private readonly TextBlock caption;
        public bool On;

        public Tile(string icon, string title, Action click)
        {
            CornerRadius = new CornerRadius(20);
            Background = fill;
            Height = 78;
            Margin = new Thickness(5);
            Padding = new Thickness(14, 12, 12, 10);
            StackPanel stack = new StackPanel();
            System.Windows.Shapes.Path glyph = Ui.Icon(icon, 22, ink);
            glyph.HorizontalAlignment = HorizontalAlignment.Left;
            stack.Children.Add(glyph);
            caption = Ui.Text(title, 13, 255, FontWeights.SemiBold);
            caption.Foreground = ink;
            caption.Margin = new Thickness(0, 9, 0, 0);
            stack.Children.Add(caption);
            Child = stack;
            Ui.Pressable(this, click);
        }

        public void Set(bool on)
        {
            On = on;
            Duration time = TimeSpan.FromMilliseconds(200);
            fill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(on ? Ui.White(235) : Ui.White(28), time));
            ink.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(
                on ? Color.FromRgb(20, 20, 24) : Colors.White, time));
        }
    }

    /// <summary>Толстый ползунок со значком внутри, как у громкости на iPhone.</summary>
    internal sealed class GlassSlider : Grid
    {
        private readonly Border bar = new Border();
        public double Value;
        public event Action<double> Changed;

        public GlassSlider(string icon)
        {
            Height = 42;
            Margin = new Thickness(5);
            Cursor = Cursors.Hand;
            Background = Brushes.Transparent;
            Border track = new Border { CornerRadius = new CornerRadius(21), Background = Ui.Brush(28) };
            Children.Add(track);
            bar.CornerRadius = new CornerRadius(21);
            bar.Background = Ui.Brush(225);
            bar.HorizontalAlignment = HorizontalAlignment.Left;
            bar.MinWidth = 42;
            Children.Add(bar);
            System.Windows.Shapes.Path glyph = Ui.Icon(icon, 18, new SolidColorBrush(Color.FromArgb(200, 110, 110, 118)));
            glyph.HorizontalAlignment = HorizontalAlignment.Left;
            glyph.Margin = new Thickness(13, 0, 0, 0);
            glyph.IsHitTestVisible = false;
            Children.Add(glyph);
            SizeChanged += delegate { Draw(); };
            MouseLeftButtonDown += (s, e) => { CaptureMouse(); Drag(e); };
            MouseMove += (s, e) => { if (IsMouseCaptured) Drag(e); };
            MouseLeftButtonUp += (s, e) => ReleaseMouseCapture();
        }

        private void Drag(MouseEventArgs e)
        {
            double usable = Math.Max(1, ActualWidth - 42);
            Value = Math.Max(0, Math.Min(1, (e.GetPosition(this).X - 21) / usable));
            Draw();
            Action<double> handler = Changed;
            if (handler != null) handler(Value);
        }

        public void Set(double value)
        {
            Value = value;
            Draw();
        }

        private void Draw()
        {
            bar.Width = 42 + Math.Max(0, ActualWidth - 42) * Value;
        }
    }
}
