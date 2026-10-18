using QingtingRadio.Services;
using QingtingRadio.ViewModels;
using System.ComponentModel;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace QingtingRadio
{
    public sealed partial class VolumeControl : UserControl, INotifyPropertyChanged
    {
        private MediaPlayer Player => PlaybackService.Instance.Player;
        private RadioSettingsViewModel Settigns => RadioSettingsViewModel.Instance;
        public VolumeControl()
        {
            this.InitializeComponent();
            this.Loaded += VolumeControl_Loaded;
            this.Unloaded += VolumeControl_Unloaded;
     
        }

        private void VolumeControl_Unloaded(object sender, RoutedEventArgs e)
        {

        }

        // 加载时同步滑杆位置会触发 ValueChanged 回调，再走 Volume setter。
        // 而 Volume setter 带"音量>0 就取消静音"的逻辑，会让"打开音量控件"这个动作
        // 悄悄清掉用户手动设置的静音状态，必须用标志跳过加载阶段的回调。
        private bool _initializingVolume;

        private void VolumeControl_Loaded(object sender, RoutedEventArgs e)
        {
            _initializingVolume = true;
            try
            {
                volumeSlider.Value = Settigns.Volume;
            }
            finally
            {
                _initializingVolume = false;
            }

            ChangeVolumeSymbol(Player.IsMuted);
        }

        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_initializingVolume) return;

            Volume = e.NewValue;
        }

        private void VolumeControl_Click(object sender, RoutedEventArgs e)
        {
            IsMuted = !Player.IsMuted; 
        }

        public bool IsMuted
        {
            get { return Player.IsMuted; }
            set
            {
                Player.IsMuted = value;
                ChangeVolumeSymbol(value);
            }
        }

        public double Volume
        {
            get { return Settigns.Volume; }
            set
            {
                // 音量为 0 自动静音，调高自动取消静音（注意：加载阶段已用 _initializingVolume 跳过，避免误取消静音）
                IsMuted = value <= 0;
                volumeSlider.Value = value;
                Settigns.Volume = value;
                OnPropertyChanged(nameof(Volume));
            }
        }

        private void  ChangeVolumeSymbol(bool value)
        {
            if (value)
            {
                VolumeValue.Content = "\xE74F";
            }
            else
            {
                VolumeValue.Content = "\xE767";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
