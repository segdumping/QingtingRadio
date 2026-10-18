using QingtingRadio.DataModels;
using QingtingRadio.Services;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace QingtingRadio
{
    public sealed partial class PlayControl : UserControl
    {
        private MediaPlayer Player = PlaybackService.Instance.Player;
        private MediaListViewModel MediaList = MediaListViewModel.Instance;
        private RadioPlayPanelViewModel PlayingPanel => RadioPlayPanelViewModel.Instance;

        public PlayControl()
        {
            this.InitializeComponent();
            this.Loaded += PlayControl_Loaded;
            this.Unloaded += PlayControl_Unloaded;
        }

        private void PlayControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Player.PlaybackSession.PlaybackStateChanged -= Player_CurrentStateChanged;
            PlayingPanel.PropertyChanged -= PlayingPanel_PropertyChanged;
        }

        private void PlayControl_Loaded(object sender, RoutedEventArgs e)
        {
            Player.PlaybackSession.PlaybackStateChanged += Player_CurrentStateChanged;
            PlayingPanel.PropertyChanged += PlayingPanel_PropertyChanged;

            Player_CurrentStateChanged(Player.PlaybackSession, null);
        }

        private void PlayingPanel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("IsBuffering"))
            {
                BufferRing.IsActive = (sender as RadioPlayPanelViewModel).IsBuffering;
            }
        }

        private async void Player_CurrentStateChanged(MediaPlaybackSession sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (sender.PlaybackState)
                {
                    case MediaPlaybackState.Playing:
                        Play.Content = "\xE103";
                        RingStatusChange(false);
                        // 同步复位 VM 里的缓冲标志：否则 IsBuffering 会一直停留在 true，
                        // 与界面状态不一致，下次判断/绑定都可能出错。
                        PlayingPanel.IsBuffering = false;
                        break;
                    case MediaPlaybackState.Paused:
                        Play.Content = "\xE102";
                        break;
                    default:
                        break;
                }
            });
        }

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            await RadioPlayViewModel.Instance.TogglePlayPauseAsync();
        }

        private async void RingStatusChange(bool value)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                BufferRing.IsActive = value;
            });
        }
    }
}
