using System;
using Windows.Media;
using Windows.Media.Playback;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;

namespace QingtingRadio.Services
{
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
        /// 由 ButtonPressed 转发给 MediaListViewModel 的换曲逻辑（含专辑列表按需加载）。
        /// MediaListViewModel.Instance 必须延迟到按下时再取，否则会造成单例初始化互相递归。
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
            // SMTC 的按钮回调可能来自后台线程。SkipNext/SkipPrevious 会增删播放列表条目
            // 并触发绑定到界面的 PropertyChanged，必须在 UI 线程上执行，否则存在跨线程访问风险。
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
                    // 后台线程访问 Window.Current 会抛异常，忽略并走下面的兜底
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
