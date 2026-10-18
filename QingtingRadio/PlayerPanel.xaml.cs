using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using QingtingRadio.DataModels;
using QingtingRadio.Services;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;

namespace QingtingRadio
{
    public sealed partial class PlayerPanel : UserControl
    {
        private MediaPlayer Player { get; }
        private RadioPlayPanelViewModel PlayingPanel => RadioPlayPanelViewModel.Instance;
        private MediaListViewModel MediaList = MediaListViewModel.Instance;
        private RadioSettingsViewModel Settigns => RadioSettingsViewModel.Instance;

        public PlayerPanel()
        {
            this.InitializeComponent();
            this.Loaded += PlayerPanel_Loaded;
            this.Unloaded += PlayerPanel_Unloaded;

            this.DataContext = PlayingPanel;
            Player = PlaybackService.Instance.Player;
        }

        private void PlayerPanel_Loaded(object sender, RoutedEventArgs e)
        {
            Player.IsMutedChanged += Player_IsMutedChanged;
            MediaList.PlaybackList.ItemFailed += PlaybackList_ItemFailed;
            MediaList.PlaybackList.CurrentItemChanged += PlaybackList_CurrentItemChanged;

            Player.PlaybackSession.PlaybackStateChanged += Player_CurrentStateChanged;
            Player.PlaybackSession.BufferingStarted += Player_BufferingStarted;
            Player.PlaybackSession.BufferingEnded += Player_BufferingEnded;

            NetworkManager.Current.NetworkStatusChanged += Current_NetworkStatusChanged;

            InitPlayerPanel();
        }

        private async void Current_NetworkStatusChanged(object sender)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!AppHelper.Current.NotifyNetState())
                {
                    Player.Pause();
                }
            });       
        }

        private void PlayerPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            Player.IsMutedChanged -= Player_IsMutedChanged;
            MediaList.PlaybackList.ItemFailed -= PlaybackList_ItemFailed;
            MediaList.PlaybackList.CurrentItemChanged -= PlaybackList_CurrentItemChanged;
            Player.PlaybackSession.PlaybackStateChanged -= Player_CurrentStateChanged;
            Player.PlaybackSession.BufferingStarted -= Player_BufferingStarted;
            Player.PlaybackSession.BufferingEnded -= Player_BufferingEnded;

            NetworkManager.Current.NetworkStatusChanged -= Current_NetworkStatusChanged;
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

        private async void PlaybackList_CurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            // 快照持久化已移到 SaveNowPlayingSnapshotAsync,回调体内无需 await
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
           {
               try
               {
                   if (MediaList.MediaType != MusicItem.AudioType.Live)
                   {
                       if (args.NewItem == null) return;
                       var displayProperties = args.NewItem.GetDisplayProperties();
                       if (!displayProperties.MusicProperties.Title.Equals("未知") && !string.IsNullOrEmpty(displayProperties.MusicProperties.Title))
                       {
                           RadioPlayPanelViewModel.Instance.NowPlaying = displayProperties.MusicProperties.Title;
                       }

                       try
                       {
                           object positionValue = null;
                           if (args.NewItem.Source.CustomProperties.TryGetValue("position", out positionValue) &&
                               positionValue != null)
                           {
                               Player.PlaybackSession.Position = TimeSpan.FromSeconds(Convert.ToDouble(positionValue));
                           }
                       }
                       catch(Exception ex)
                       {
                           LogManager.LogAsync("set playbacklist current item position fail", ex);
                           Debug.WriteLine(ex.ToString());
                       }

                       MediaList.SelectedIndex = (int)MediaList.PlaybackList.CurrentItemIndex;

                       // 快照持久化移出 UI 回调：读改写 JSON 在后台线程执行，不阻塞切台
                       _ = SaveNowPlayingSnapshotAsync(args.NewItem, displayProperties.MusicProperties.Title);
                   }
               }
               catch(Exception ex)
               {
                   LogManager.LogAsync("playbacklist current item changed", ex);
               }
           });
        }

        private static async Task SaveNowPlayingSnapshotAsync(MediaPlaybackItem newItem, string playingTitle)
        {
            try
            {
                LastestPlay item = await LastestPlay.LoadCurrentAsync();
                if (item == null || newItem == null || newItem.Source == null) return;

                var props = newItem.Source.CustomProperties;

                object rawMediaId = null;
                object rawFilePath = null;
                object rawLocalName = null;
                props.TryGetValue("mediaId", out rawMediaId);
                props.TryGetValue("filepath", out rawFilePath);
                props.TryGetValue("localFileName", out rawLocalName);

                string mediaId = rawMediaId?.ToString();
                string filepath = rawFilePath?.ToString();
                string localName = rawLocalName?.ToString();

                // 本地缓存文件是用 CreateFromStream 播放的，Source.Uri 为 null；
                // 此时 filepath 里存的是远端播放地址。直接把 Uri 解引用会抛 NullReferenceException。
                Uri uri = newItem.Source.Uri;
                bool isRemote = uri != null &&
                    (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                     uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(mediaId))
                {
                    item.Id = mediaId;
                }
                item.Playing = playingTitle;

                if (isRemote)
                {
                    // 远端播放：Uri 是播放地址，filepath/localFileName 是本地缓存文件名
                    item.PlayUri = uri.ToString();
                    if (!string.IsNullOrEmpty(localName))
                    {
                        item.FileName = localName;
                    }
                    else if (!string.IsNullOrEmpty(filepath))
                    {
                        item.FileName = filepath;
                    }
                }
                else
                {
                    // 本地缓存流播放：Uri 为空，filepath 存的是远端播放地址
                    if (!string.IsNullOrEmpty(filepath))
                    {
                        item.PlayUri = filepath;
                    }
                    if (!string.IsNullOrEmpty(localName))
                    {
                        item.FileName = localName;
                    }
                    // localName 缺失时保留快照里已有的文件名，不清空
                }

                await LastestPlay.SaveCurrentAsync(item);
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("save now playing snapshot", ex);
            }
        }

        private async void PlaybackList_ItemFailed(MediaPlaybackList sender, MediaPlaybackItemFailedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // 播放失败时复位图标、缓冲圈与 IsBuffering，让用户可以点击重播
                PlayMessageNotify("播放失败");
                RingStatusChange(false);
                RadioPlayPanelViewModel.Instance.IsBuffering = false;
                playbuttonicon.Symbol = Symbol.Play;
            });
        }

        private void Player_BufferingEnded(MediaPlaybackSession sender, object args)
        {
            RingStatusChange(false);
        }

        private void Player_BufferingStarted(MediaPlaybackSession sender, object args)
        {
            RingStatusChange(true);
        }

        private async void Player_CurrentStateChanged(MediaPlaybackSession sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (sender.PlaybackState)
                {
                    case MediaPlaybackState.Playing:
                        playbuttonicon.Symbol = Symbol.Pause;
                        RingStatusChange(false);
                        break;
                    case MediaPlaybackState.Paused:
                        playbuttonicon.Symbol = Symbol.Play;
                        break;
                    default:
                        break;
                }
            });
        }

        private async void playButton_Click(object sender, RoutedEventArgs e)
        {
            await RadioPlayViewModel.Instance.TogglePlayPauseAsync();
        }

        private async void InitPlayerPanel()
        {
            LastestPlay item = await LastestPlay.LoadCurrentAsync();
            if(item == null)
            {
                PlayingPanel.ImageUri = "ms-appx:///Assets/recommend_defaultbg.png";
                PlayingPanel.Title = "蜻蜓电台";
                PlayingPanel.NowPlaying = "欢迎收听";
                PlayingPanel.PlayID = "default";
                    
            }
            else
            {
                PlayingPanel.ImageUri = string.IsNullOrEmpty(item.ImageUri) ? "ms-appx:///Assets/recommend_defaultbg.png" : item.ImageUri;
                PlayingPanel.Title = string.IsNullOrEmpty(item.Title) ? "蜻蜓电台" : item.Title;
                PlayingPanel.NowPlaying = string.IsNullOrEmpty(item.Playing) ? "欢迎收听" : item.Playing;
                PlayingPanel.PlayID = item.Id;
            }
            
            Player.Volume = Settigns.Volume / 100;
        }

        private async void RingStatusChange(bool value)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                PlayRing.IsActive = value;
            });
        }

        private async void PlayMessageNotify(string msg)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                NotifyUser notifyPopup = new NotifyUser(msg);
                notifyPopup.Show();
            });
        }

        private void Replay_Click(object sender, RoutedEventArgs e)
        {
            MediaListViewModel mediaList = MediaListViewModel.Instance;

            if (mediaList.MediaType ==  MusicItem.AudioType.Album || mediaList.MediaType == MusicItem.AudioType.Program)
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

        private void PlayImage_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            MainPage.Current.ContentFrame.Navigate(typeof(PlayPage));
        }

        private void PlayTitle_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            MainPage.Current.ContentFrame.Navigate(typeof(PlayPage));
        }
    }

    class ThumbProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            TimeSpan time = TimeSpan.FromSeconds(System.Convert.ToDouble(value.ToString()));
            string newvalue = "";
            if(time.Hours > 0)
            {
                newvalue += time.Hours.ToString() + ":";
            }
            if (time.Minutes > 0)
            {
                newvalue += time.Minutes.ToString() + ":";
            }

            newvalue += time.Seconds.ToString();
            
            return newvalue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
