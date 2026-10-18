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
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace QingtingRadio
{
    /// <summary>
    /// 播放进度条控件：拖动 Seek、缓冲圈与直播标题刷新。
    /// </summary>
    public sealed partial class ProgressControl : UserControl, INotifyPropertyChanged
    {
        private MediaPlayer Player = PlaybackService.Instance.Player;
        private MediaListViewModel MediaList = MediaListViewModel.Instance;
        private RadioPlayPanelViewModel PlayingPanel => RadioPlayPanelViewModel.Instance;

        private long _oldTicks;
        private TimeSpan PlayedTime;
        private string _position;
        private string _duration;
        private bool _isLargeControlVisibility;
        private bool _isPlayPageLoaded;
        // 用户是否正按住滑块拖动：拖动期间程序性位置回写暂停，松手时才真正 Seek
        private bool _isDragging;
        private double _pendingSeek = -1;
        // 直播节目结束后的标题刷新只允许一个在途请求，避免每秒重复打接口
        private bool _liveTitleRefreshInFlight;

        public ProgressControl()
        {
            this.InitializeComponent();
            this.Loaded += ProgressControl_Loaded;
            this.Unloaded += ProgressControl_Unloaded;
        }

        private void ProgressControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Player.PlaybackSession.PositionChanged -= PlaybackSession_PositionChanged;
            Player.PlaybackSession.NaturalDurationChanged -= PlaybackSession_NaturalDurationChanged;
            RadioPlayPanelViewModel.Instance.PropertyChanged -= PlayPanel_PropertyChanged;
        }

        private void ProgressControl_Loaded(object sender, RoutedEventArgs e)
        {
            Player.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
            Player.PlaybackSession.NaturalDurationChanged += PlaybackSession_NaturalDurationChanged;
            RadioPlayPanelViewModel.Instance.PropertyChanged += PlayPanel_PropertyChanged;

            PlaybackSession_NaturalDurationChanged(null, null);
        }

        private void PlaybackSession_NaturalDurationChanged(MediaPlaybackSession sender, object args)
        {
            NaturalDurationChanged();
        }

        private void PlaybackSession_PositionChanged(MediaPlaybackSession sender, object args)
        {
            PositionChanged();         
        }

        private async void NaturalDurationChanged()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (PlayingPanel.IsReplay)
                {
                    TimeSpan duration = Player.PlaybackSession.NaturalDuration;
                    progressSlider.Maximum = Player.PlaybackSession.NaturalDuration.TotalSeconds;
                    Duration = (duration.Hours > 0 ? duration.ToString(@"hh\:mm\:ss") : duration.ToString(@"mm\:ss"));
                    progressSlider.IsEnabled = true;
                }
                else
                {
                    try
                    {
                        string itemId = MediaList.CurrentMediaId;
                        if (itemId == null) return;
                        ReplayRadio item = await PlayInfoHelper.Current.CurrentPlaying(itemId);
                        if (item == null) return;

                        DateTime startDate = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + item.StartTime);
                        TimeSpan tmpSpan = TimeSpan.FromSeconds(double.Parse(item.Duration));

                        Duration = (tmpSpan.Hours > 0 ? tmpSpan.ToString(@"hh\:mm\:ss") : tmpSpan.ToString(@"mm\:ss"));

                        PlayedTime = startDate.TimeOfDay;
                        progressSlider.Maximum = tmpSpan.TotalSeconds;

                    }
                    catch(Exception ex)
                    {
                        LogManager.LogAsync("natural duration changed", ex);
                    }

                    progressSlider.IsEnabled = false;
                }
            });
        }

        private async void PositionChanged()
        {
            if ((DateTime.Now.Ticks - _oldTicks) < 10000000) return;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (PlayingPanel.IsReplay)
                {
                    TimeSpan position = Player.PlaybackSession.Position;
                    Position = (position.Hours > 0 ? position.ToString(@"hh\:mm\:ss") : position.ToString(@"mm\:ss"));

                    // 用户拖动期间不回写滑块，否则会覆盖用户正在拖到的位置
                    if (!_isDragging)
                    {
                        progressSlider.Value = position.TotalSeconds;
                    }
                }
                else
                {
                    TimeSpan tmp = DateTime.Now.TimeOfDay - PlayedTime;
                    if (tmp.TotalSeconds >= progressSlider.Maximum && !_liveTitleRefreshInFlight)
                    {
                        _liveTitleRefreshInFlight = true;
                        try
                        {
                            PlayingPanel.NowPlaying = await PlayInfoHelper.Current.CurrentPlayingTitle(MediaList.CurrentMediaId);
                            PlaybackSession_NaturalDurationChanged(null, null);
                        }
                        finally
                        {
                            _liveTitleRefreshInFlight = false;
                        }
                    }

                    progressSlider.Value = tmp.TotalSeconds;

                    Position = (tmp.Hours > 0 ? tmp.ToString(@"hh\:mm\:ss") : tmp.ToString(@"mm\:ss"));
                }

                _oldTicks = DateTime.Now.Ticks;
            });
        }

        private void progressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            // 拖动过程中只记录目标位置，松手后才统一 Seek，避免拖动中每个变化都触发一次跳转
            if (_isDragging)
            {
                _pendingSeek = e.NewValue;
            }
        }

        private void PlayPanel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("LargeVisibility"))
            {
                Visibility largeVisibility = (sender as RadioPlayPanelViewModel).LargeVisibility;
                _isLargeControlVisibility = largeVisibility == Visibility.Collapsed ? false : true;
            }
            else
            { }
        }

        public string Position
        {
            get { return _position; }
            set
            {
                if(_position != value)
                {
                    _position = value;
                    OnPropertyChanged(nameof(Position));
                }
            }
        }

        public string Duration
        {
            get { return _duration; }
            set
            {
                if (_duration != value && !string.IsNullOrEmpty(value))
                {
                    _duration = value;
                    OnPropertyChanged(nameof(Duration));
                }
            }
        }

        public bool IsPlayPageLoaded
        {
            get { return _isPlayPageLoaded; }
            set
            {
                if (_isPlayPageLoaded != value)
                {
                    _isPlayPageLoaded = value;
                    OnPropertyChanged(nameof(IsPlayPageLoaded));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void progressSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = true;
            _pendingSeek = -1;
            progressSlider.CapturePointer(e.Pointer);
        }

        private void progressSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;
            progressSlider.ReleasePointerCapture(e.Pointer);

            if (_pendingSeek >= 0 && PlayingPanel.IsReplay)
            {
                Player.PlaybackSession.Position = TimeSpan.FromSeconds(_pendingSeek);
            }
            _pendingSeek = -1;
        }
    }
}