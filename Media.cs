using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;

namespace GlassIntro
{
    /// <summary>Читает и управляет тем, что сейчас играет в Windows: Spotify, браузер, плееры.</summary>
    internal sealed class MediaController
    {
        private readonly Dispatcher dispatcher;
        private GlobalSystemMediaTransportControlsSessionManager manager;
        private GlobalSystemMediaTransportControlsSession session;
        private TimeSpan position;
        private DateTimeOffset positionTime;

        public string Title = "";
        public string Artist = "";
        public BitmapImage Art;
        public bool Playing;
        public TimeSpan Duration;

        public event Action Changed;
        public event Action TrackChanged;

        public MediaController(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public bool HasMedia { get { return session != null && Title.Length > 0; } }

        public TimeSpan Position
        {
            get
            {
                TimeSpan p = position;
                if (Playing) p += DateTimeOffset.Now - positionTime;
                if (Duration > TimeSpan.Zero && p > Duration) p = Duration;
                return p < TimeSpan.Zero ? TimeSpan.Zero : p;
            }
        }

        public async Task StartAsync()
        {
            try
            {
                manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                manager.CurrentSessionChanged += delegate { Post(Bind); };
                Bind();
            }
            catch (Exception e)
            {
                Program.Log(e);
            }
        }

        private void Post(Action action)
        {
            dispatcher.BeginInvoke(action);
        }

        private void Bind()
        {
            if (session != null)
            {
                session.MediaPropertiesChanged -= OnMedia;
                session.PlaybackInfoChanged -= OnPlayback;
                session.TimelinePropertiesChanged -= OnTimeline;
            }
            session = manager.GetCurrentSession();
            if (session == null)
            {
                Title = "";
                Artist = "";
                Art = null;
                Playing = false;
                Raise(Changed);
                return;
            }
            session.MediaPropertiesChanged += OnMedia;
            session.PlaybackInfoChanged += OnPlayback;
            session.TimelinePropertiesChanged += OnTimeline;
            ReadPlayback();
            ReadTimeline();
            var ignored = ReadMediaAsync();
        }

        private void OnMedia(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e)
        {
            Post(delegate { var ignored = ReadMediaAsync(); });
        }

        private void OnPlayback(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e)
        {
            Post(delegate { ReadPlayback(); Raise(Changed); });
        }

        private void OnTimeline(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e)
        {
            Post(delegate { ReadTimeline(); Raise(Changed); });
        }

        private void ReadPlayback()
        {
            try
            {
                Playing = session != null && session.GetPlaybackInfo().PlaybackStatus
                    == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            }
            catch (Exception e)
            {
                Program.Log(e);
            }
        }

        private void ReadTimeline()
        {
            try
            {
                if (session == null) return;
                GlobalSystemMediaTransportControlsSessionTimelineProperties t = session.GetTimelineProperties();
                position = t.Position;
                positionTime = t.LastUpdatedTime;
                Duration = t.EndTime;
            }
            catch (Exception e)
            {
                Program.Log(e);
            }
        }

        private async Task ReadMediaAsync()
        {
            try
            {
                GlobalSystemMediaTransportControlsSession current = session;
                if (current == null) return;
                GlobalSystemMediaTransportControlsSessionMediaProperties p = await current.TryGetMediaPropertiesAsync();
                if (p == null || current != session) return;
                string title = p.Title ?? "";
                bool newTrack = title != Title;
                Title = title;
                Artist = p.Artist ?? "";
                Art = null;
                if (p.Thumbnail != null)
                {
                    using (var stream = await p.Thumbnail.OpenReadAsync())
                    using (MemoryStream memory = new MemoryStream())
                    {
                        await stream.AsStreamForRead().CopyToAsync(memory);
                        memory.Position = 0;
                        BitmapImage image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.DecodePixelWidth = 200;
                        image.StreamSource = memory;
                        image.EndInit();
                        image.Freeze();
                        Art = image;
                    }
                }
                ReadTimeline();
                Raise(Changed);
                if (newTrack && title.Length > 0) Raise(TrackChanged);
            }
            catch (Exception e)
            {
                Program.Log(e);
            }
        }

        private static void Raise(Action handler)
        {
            if (handler != null) handler();
        }

        public void PlayPause()
        {
            if (session != null) Ignore(session.TryTogglePlayPauseAsync().AsTask());
        }

        public void Next()
        {
            if (session != null) Ignore(session.TrySkipNextAsync().AsTask());
        }

        public void Previous()
        {
            if (session != null) Ignore(session.TrySkipPreviousAsync().AsTask());
        }

        private static void Ignore(Task task)
        {
            task.ContinueWith(t => Program.Log(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
