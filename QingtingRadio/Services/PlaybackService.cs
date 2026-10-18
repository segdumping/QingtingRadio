using System;
using Windows.Media;
using Windows.Media.Playback;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;

namespace QingtingRadio.Services
{
    /// <summary>
    /// 全局唯一 MediaPlayer 的单例容器（后台播放与 SMTC 按钮接管）。
    /// </summary>
    class PlaybackService
    {
        public MediaPlayer Player { get; private set; }

        private PlaybackService()
        {
            Player = new MediaPlayer();
            ConfigureSmtcButtons();
        }

        /// <summary>
        /// 接管系统媒体控件的上一曲/下一曲按钮：关闭 CommandManager 的自动处理，
        /// </summary>
        private void ConfigureSmtcButtons()
        {
            try
            {
                Player.CommandManager.NextBehavior.EnablingRule = MediaCommandEnablingRule.Never;
                Player.CommandManager.PreviousBehavior.EnablingRule = MediaCommandEnablingRule.Never;

                SystemMediaTransportControls smtc = Player.SystemMediaTransportControls;
                smtc.IsEnabled = true;
                smtc.IsNextEnabled = true;
                smtc.IsPreviousEnabled = true;
                smtc.ButtonPressed += Smtc_ButtonPressed;
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("configure smtc buttons", ex);
            }
        }

        private void Smtc_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Next:
                    RunOnUiThread(() => MediaListViewModel.Instance.SkipNext());
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    RunOnUiThread(() => MediaListViewModel.Instance.SkipPrevious());
                    break;
            }
        }

        /// <summary>
        /// 把动作调度回 UI 线程执行。取不到 UI 调度器时退化为当前线程直接执行，保证功能不丢。
        /// </summary>
        private static void RunOnUiThread(Action action)
        {
            if (action == null) return;

            try
            {
                Windows.UI.Core.CoreDispatcher dispatcher = null;

                try
                {
                    var window = Windows.UI.Xaml.Window.Current;
                    if (window != null)
                    {
                        dispatcher = window.Dispatcher;
                    }
                }
                catch (Exception)
                {
                  
                }

                if (dispatcher == null)
                {
                    dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView?.CoreWindow?.Dispatcher;
                }

                if (dispatcher == null || dispatcher.HasThreadAccess)
                {
                    action();
                    return;
                }

                _ = dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => action());
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("smtc marshal to ui thread", ex);
                try
                {
                    action();
                }
                catch (Exception inner)
                {
                    LogManager.LogAsync("smtc button action", inner);
                }
            }
        }

        public static readonly PlaybackService Instance = new PlaybackService();
    }
}
