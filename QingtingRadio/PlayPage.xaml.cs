using QingtingRadio.DataModels;
using QingtingRadio.Services;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;
using System;
using System.ComponentModel;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace QingtingRadio
{
    public sealed partial class PlayPage : Page
    {
        private MediaPlayer Player => PlaybackService.Instance.Player;
        private MediaListViewModel MediaList = MediaListViewModel.Instance;
        private RadioSettingsViewModel Settigns => RadioSettingsViewModel.Instance;
        private RadioPlayPanelViewModel PlayingPanel => RadioPlayPanelViewModel.Instance;

        public PlayPage()
        {
            this.InitializeComponent();
            this.Loaded += PlayPage_Loaded; 
            this.Unloaded += PlayPage_Unloaded;

            this.DataContext = PlayingPanel;
        }

        private void PlayPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Player.IsMutedChanged -= Player_IsMutedChanged;
            Settigns.PropertyChanged -= PlayPanel_PropertyChanged;
            PlayingPanel.PropertyChanged -= PlayPanel_PropertyChanged;
            Player.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;

            Progress.IsPlayPageLoaded = false;
        }

        private void PlayPage_Loaded(object sender, RoutedEventArgs e)
        {
            Player.IsMutedChanged += Player_IsMutedChanged;
            Settigns.PropertyChanged += PlayPanel_PropertyChanged;
            PlayingPanel.PropertyChanged += PlayPanel_PropertyChanged;

            Player.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;

            PlayPanel_PropertyChanged(null, new PropertyChangedEventArgs("AppTheme"));
            if (Player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                EllStoryboard.Begin();
            }

            Progress.IsPlayPageLoaded = true;

            SetThumb();
        }

        private async void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (sender.PlaybackState)
                {
                    case MediaPlaybackState.Playing:
                        if (EllStoryboard.GetCurrentState() == ClockState.Stopped)
                        {
                            EllStoryboard.Begin();
                        }
                        else
                        {
                            EllStoryboard.Resume();
                        }
                        break;
                    case MediaPlaybackState.Paused:
                        if (EllStoryboard.GetCurrentState() == ClockState.Active)
                        {
                            EllStoryboard.Pause();
                        }                       
                        break;
                    default:
                        break;
                }
            });
        }

        private async void SetThumb()
        {
            try
            {
                BitmapImage bmp = new BitmapImage();
                string thumbUri = await PlayInfoHelper.Current.GetMediaThumb(MediaList.CurrentMediaId);
                if (thumbUri == null)
                {
                    if (string.IsNullOrEmpty(PlayingPanel.ImageUri)) return;
                    bmp.UriSource = new Uri(PlayingPanel.ImageUri);
                }
                else
                {
                    bmp.UriSource = new Uri(thumbUri);
                }

                Thumb.ImageSource = bmp;
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("set thumb", ex);
            }
        }
        private async void Player_IsMutedChanged(MediaPlayer sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                VolumeValue.IsMuted = Player.IsMuted;
                if (Player.IsMuted)
                {
                    Volume.Content = "\xE74F";
                }
                else
                {
                    Volume.Content = "\xE767";
                }
            });
        }

        private void PlayPanel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("LargeVisibility"))
            {
                Visibility largeVisibility = (sender as RadioPlayPanelViewModel).LargeVisibility;
                if(largeVisibility == Visibility.Visible)
                {
                    if (this.Frame.SourcePageType.ToString().Contains("PlayPage") && this.Frame.CanGoBack)
                    {
                        this.Frame.GoBack();
                    }
                }
            }
            else if(e.PropertyName.Equals("NowPlaying"))
            {
                if (this.Frame.SourcePageType.ToString().Contains("PlayPage"))
                {
                    TitleName.Text = PlayingPanel.NowPlaying;
                }
            }
            else if (e.PropertyName.Equals("AppTheme"))
            {
                if (Settigns.AppTheme)
                {
                    Thumb.Opacity = 0.5;
                }
                else
                {
                    Thumb.Opacity = 1;
                }
            }
        }

        private void Replay_Click(object sender, RoutedEventArgs e)
        {
            MediaListViewModel mediaList = MediaListViewModel.Instance;

            if (mediaList.MediaType == MusicItem.AudioType.Album || mediaList.MediaType == MusicItem.AudioType.Program)
            {
                // 专辑/节目：进入独立节目列表页面
                MainPage.Current.ContentFrame.Navigate(typeof(ReplayPage), "album");
            }
            else
            {
                // 电台/回听：进入独立回听页面
                MainPage.Current.ContentFrame.Navigate(typeof(ReplayPage));
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            TitleName.Text = PlayingPanel.NowPlaying;
            RadioSingleProperty.Current.PanelVisibility = Visibility.Collapsed;             
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            RadioSingleProperty.Current.PanelVisibility = Visibility.Visible;
        }
    }
}
