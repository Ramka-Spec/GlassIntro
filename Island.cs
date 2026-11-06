using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GlassIntro
{
    /// <summary>Мини-плеер у верхнего края экрана: капсула раскрывается при наведении и при смене трека.</summary>
    internal sealed class Island : GlassWindow
    {
        private const double FullWidth = 410, FullHeight = 136;
        private readonly MediaController media;
        private readonly GlassEngine engine;
        private readonly Grid compact = new Grid();
        private readonly Grid full = new Grid();
        private readonly Border smallArt = new Border();
        private readonly Border bigArt = new Border();
        private readonly TextBlock smallTitle = Ui.Text("", 13, 255, FontWeights.SemiBold);
        private readonly TextBlock bigTitle = Ui.Text("", 16, 255, FontWeights.SemiBold);
        private readonly TextBlock artist = Ui.Text("", 13, 185);
        private readonly TextBlock elapsed = Ui.Text("0:00", 11, 170);
        private readonly TextBlock remaining = Ui.Text("0:00", 11, 170);
        private readonly Border progress = new Border();
        private readonly Grid progressTrack = new Grid();
        private readonly System.Windows.Shapes.Path playIcon = Ui.Icon(Icons.Play, 26, Brushes.White);
        private readonly ScaleTransform[] bars = new ScaleTransform[4];
        private readonly DispatcherTimer peekTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        private readonly DispatcherTimer progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        private bool hovered;
        private bool peeking;
        private bool suppressed;
        private bool barsRunning;
        private bool shown;

        public Island(MediaController media, GlassEngine engine)
            : base(FullWidth + 8, FullHeight + 8, HorizontalAlignment.Center, VerticalAlignment.Top, false)
        {
            this.media = media;
            this.engine = engine;
            Title = "GlassIntro Mini Player";
            BuildCompact();
            BuildFull();
            Host.Children.Add(compact);
            Host.Children.Add(full);
            full.Opacity = 0;
            full.IsHitTestVisible = false;
            Snap(150, 36, 18, 0);

            Surface.MouseEnter += delegate { hovered = true; UpdateState(); };
            Surface.MouseLeave += delegate { hovered = false; UpdateState(); };
            peekTimer.Tick += delegate { peekTimer.Stop(); peeking = false; UpdateState(); };
            progressTimer.Tick += delegate { UpdateProgress(); };
            media.Changed += Refresh;
            media.TrackChanged += OnTrackChanged;
            engine.Changed += UpdateState;
        }

        private static Border Rounded(double size, double radius)
        {
            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(radius),
                Background = Ui.Brush(30)
            };
        }

        private void BuildCompact()
        {
            compact.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            compact.ColumnDefinitions.Add(new ColumnDefinition());
            compact.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            compact.Margin = new Thickness(7, 0, 14, 0);
            smallArt.Width = 26;
            smallArt.Height = 26;
            smallArt.CornerRadius = new CornerRadius(13);
            smallArt.Background = Ui.Brush(30);
            smallArt.VerticalAlignment = VerticalAlignment.Center;
            compact.Children.Add(smallArt);
            smallTitle.Margin = new Thickness(9, 0, 9, 0);
            Grid.SetColumn(smallTitle, 1);
            compact.Children.Add(smallTitle);
            StackPanel eq = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i] = new ScaleTransform(1, 0.3);
                eq.Children.Add(new Rectangle
                {
                    Width = 3,
                    Height = 15,
                    RadiusX = 1.5,
                    RadiusY = 1.5,
                    Margin = new Thickness(1, 0, 1, 0),
                    Fill = Brushes.White,
                    RenderTransform = bars[i],
                    RenderTransformOrigin = new Point(0.5, 0.5)
                });
            }
            Grid.SetColumn(eq, 2);
            compact.Children.Add(eq);
        }

        private void BuildFull()
        {
            full.Margin = new Thickness(18, 16, 20, 14);
            full.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            full.ColumnDefinitions.Add(new ColumnDefinition());
            bigArt.Width = 104;
            bigArt.Height = 104;
            bigArt.CornerRadius = new CornerRadius(18);
            bigArt.Background = Ui.Brush(30);
            bigArt.VerticalAlignment = VerticalAlignment.Center;
            full.Children.Add(bigArt);

            Grid right = new Grid { Margin = new Thickness(16, 0, 0, 0) };
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition());
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(right, 1);
            right.Children.Add(bigTitle);
            Grid.SetRow(artist, 1);
            right.Children.Add(artist);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            buttons.Children.Add(Control(Ui.Icon(Icons.Previous, 22, Brushes.White), () => media.Previous()));
            buttons.Children.Add(Control(playIcon, () => media.PlayPause()));
            buttons.Children.Add(Control(Ui.Icon(Icons.Next, 22, Brushes.White), () => media.Next()));
            Grid.SetRow(buttons, 2);
            right.Children.Add(buttons);

            Grid time = new Grid();
            time.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            time.ColumnDefinitions.Add(new ColumnDefinition());
            time.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            time.Children.Add(elapsed);
            progressTrack.Height = 5;
            progressTrack.Margin = new Thickness(8, 0, 8, 0);
            progressTrack.Children.Add(new Border { CornerRadius = new CornerRadius(2.5), Background = Ui.Brush(55) });
            progress.CornerRadius = new CornerRadius(2.5);
            progress.Background = Ui.Brush(235);
            progress.HorizontalAlignment = HorizontalAlignment.Left;
            progressTrack.Children.Add(progress);
            Grid.SetColumn(progressTrack, 1);
            time.Children.Add(progressTrack);
            Grid.SetColumn(remaining, 2);
            time.Children.Add(remaining);
            Grid.SetRow(time, 3);
            right.Children.Add(time);
            full.Children.Add(right);
        }

        private static Border Control(UIElement icon, Action click)
        {
            Border b = new Border
            {
                Width = 44,
                Height = 38,
                Margin = new Thickness(5, 0, 5, 0),
                CornerRadius = new CornerRadius(19),
                Background = Brushes.Transparent,
                Child = icon
            };
            Ui.Pressable(b, click, 0.8);
            return b;
        }

        private void OnTrackChanged()
        {
            peeking = true;
            peekTimer.Stop();
            peekTimer.Start();
            UpdateState();
        }

        public void SetSuppressed(bool value)
        {
            if (suppressed == value) return;
            suppressed = value;
            UpdateState();
        }

        private void Refresh()
        {
            smallTitle.Text = media.Title;
            bigTitle.Text = media.Title;
            artist.Text = media.Artist;
            playIcon.Data = Geometry.Parse(media.Playing ? Icons.Pause : Icons.Play);
            Brush art = media.Art == null ? (Brush)Ui.Brush(30)
                : new ImageBrush(media.Art) { Stretch = Stretch.UniformToFill };
            smallArt.Background = art;
            bigArt.Background = art;
            SetBars(media.Playing && shown);
            UpdateProgress();
            UpdateState();
        }

        private void SetBars(bool run)
        {
            if (run == barsRunning) return;
            barsRunning = run;
            for (int i = 0; i < bars.Length; i++)
            {
                if (!run)
                {
                    bars[i].BeginAnimation(ScaleTransform.ScaleYProperty,
                        new DoubleAnimation(0.25, TimeSpan.FromMilliseconds(220)));
                    continue;
                }
                DoubleAnimation a = new DoubleAnimation(0.25, 1, TimeSpan.FromMilliseconds(330 + i * 95))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                bars[i].BeginAnimation(ScaleTransform.ScaleYProperty, a);
            }
        }

        private static string Clock(TimeSpan t)
        {
            return (int)t.TotalMinutes + ":" + t.Seconds.ToString("00");
        }

        private void UpdateProgress()
        {
            TimeSpan position = media.Position;
            TimeSpan duration = media.Duration;
            elapsed.Text = Clock(position);
            remaining.Text = duration > TimeSpan.Zero ? "-" + Clock(duration - position) : "";
            double share = duration > TimeSpan.Zero ? position.TotalSeconds / duration.TotalSeconds : 0;
            progress.Width = Math.Max(0, progressTrack.ActualWidth * Math.Min(1, share));
        }

        private void UpdateState()
        {
            bool visible = engine.settings.Player && media.HasMedia && !suppressed;
            if (!visible)
            {
                progressTimer.Stop();
                if (shown)
                {
                    shown = false;
                    SetBars(false);
                    AnimateTo(150, 36, 18, 0, true);
                }
                return;
            }
            if (!shown)
            {
                shown = true;
                Rect area = SystemParameters.WorkArea;
                Left = area.Left + (area.Width - Width) / 2;
                Top = area.Top + 6;
                Snap(150, 36, 18, 0);
                Show();
                SetBars(media.Playing);
            }
            bool expanded = hovered || peeking;
            Ui.Fade(compact, expanded ? 0 : 1, 140);
            Ui.Fade(full, expanded ? 1 : 0, expanded ? 240 : 120);
            compact.IsHitTestVisible = !expanded;
            full.IsHitTestVisible = expanded;
            if (expanded)
            {
                UpdateProgress();
                progressTimer.Start();
                AnimateTo(FullWidth, FullHeight, 38, 1);
            }
            else
            {
                progressTimer.Stop();
                AnimateTo(270, 40, 20, 1);
            }
        }
    }
}
