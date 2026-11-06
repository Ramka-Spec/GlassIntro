using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GlassIntro
{
    /// <summary>Панель управления в духе Control Center.</summary>
    internal sealed class ControlPanel : GlassWindow
    {
        private const double PanelWidth = 344;
        private readonly GlassEngine engine;
        private readonly StackPanel stack = new StackPanel { Margin = new Thickness(11, 12, 11, 12) };
        private readonly Tile glass;
        private readonly Tile player;
        private readonly Tile taskbar;
        private readonly Tile fade;
        private readonly Segments modes;
        private readonly GlassSlider strength = new GlassSlider(Icons.Drop);
        private readonly GlassSlider volume = new GlassSlider(Icons.Volume);
        private readonly TextBlock autostartLabel = Ui.Text("", 14);
        private double fullHeight;
        private DateTime hiddenAt = DateTime.MinValue;
        private bool open;

        public ControlPanel(GlassEngine engine)
            : base(PanelWidth + 8, 640, HorizontalAlignment.Right, VerticalAlignment.Bottom, true)
        {
            this.engine = engine;
            Title = "GlassIntro";

            stack.Children.Add(Heading());
            glass = new Tile(Icons.Sparkle, "Стекло", engine.ToggleEnabled);
            player = new Tile(Icons.Music, "Мини-плеер", () => engine.SetPlayer(!engine.settings.Player));
            taskbar = new Tile(Icons.Taskbar, "Панель задач", () => engine.SetTaskbar(!engine.settings.Taskbar));
            fade = new Tile(Icons.Windows, "Плавные окна", () => engine.SetFade(!engine.settings.Fade));
            stack.Children.Add(TileRow(glass, player));
            stack.Children.Add(TileRow(taskbar, fade));

            modes = new Segments(new[] { "Акрил", "Размытие", "Заголовки" },
                new[] { true, true, engine.Build >= 22621 }, i => engine.SetMode((Effect)i));
            stack.Children.Add(modes);

            strength.Changed += v => engine.SetPercent((int)Math.Round(5 + v * 25));
            stack.Children.Add(strength);
            volume.Changed += Audio.Set;
            stack.Children.Add(volume);

            stack.Children.Add(Row(autostartLabel, () =>
            {
                GlassEngine.SetAutostart(!GlassEngine.IsAutostart());
                Sync();
            }));
            stack.Children.Add(Row(Ui.Text("Не трогать последнее окно", 14), engine.ExcludeLastWindow));
            stack.Children.Add(Row(Ui.Text("Список исключений", 14), engine.OpenExcludeList));
            if (!GlassEngine.IsAdministrator())
                stack.Children.Add(Row(Ui.Text("Запустить от администратора", 14), engine.RestartAsAdmin));
            stack.Children.Add(Row(Ui.Text("Выйти и вернуть как было", 14), engine.Quit));
            Host.Children.Add(stack);

            engine.Changed += Sync;
            Deactivated += delegate { Dismiss(); };
            Sync();
            stack.Measure(new Size(PanelWidth, double.PositiveInfinity));
            fullHeight = Math.Min(632, stack.DesiredSize.Height);
            Snap(120, 60, 28, 0);
        }

        private UIElement Heading()
        {
            TextBlock t = Ui.Text("GlassIntro", 20, 255, FontWeights.SemiBold);
            t.Margin = new Thickness(7, 2, 5, 8);
            return t;
        }

        private static Grid TileRow(Tile a, Tile b)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(a);
            Grid.SetColumn(b, 1);
            g.Children.Add(b);
            return g;
        }

        private static Border Row(UIElement content, Action click)
        {
            Border b = new Border
            {
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(5, 1, 5, 1),
                Background = Ui.Brush(1),
                Child = content
            };
            b.MouseEnter += delegate { b.Background = Ui.Brush(26); };
            b.MouseLeave += delegate { b.Background = Ui.Brush(1); };
            Ui.Pressable(b, click, 0.97);
            return b;
        }

        private void Sync()
        {
            Settings s = engine.settings;
            glass.Set(s.Enabled);
            player.Set(s.Player);
            taskbar.Set(s.Taskbar);
            fade.Set(s.Fade);
            modes.Select((int)s.Mode);
            strength.Set((s.Percent - 5) / 25.0);
            autostartLabel.Text = (GlassEngine.IsAutostart() ? "✓  " : "") + "Запускать вместе с Windows";
        }

        public void Toggle()
        {
            if (open) { Dismiss(); return; }
            if ((DateTime.Now - hiddenAt).TotalMilliseconds < 300) return;
            Rect area = SystemParameters.WorkArea;
            Left = area.Right - Width - 4;
            Top = area.Bottom - Height - 4;
            double level = Audio.Get();
            volume.IsEnabled = level >= 0;
            volume.Set(Math.Max(0, level));
            Sync();
            open = true;
            Snap(120, 60, 28, 0);
            Show();
            Activate();
            AnimateTo(PanelWidth, fullHeight, 28, 1);
        }

        private void Dismiss()
        {
            if (!open) return;
            open = false;
            hiddenAt = DateTime.Now;
            AnimateTo(120, 60, 28, 0, true);
        }
    }
}
