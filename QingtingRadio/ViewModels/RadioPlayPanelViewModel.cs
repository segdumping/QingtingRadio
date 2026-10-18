using QingtingRadio.Services;
using System;
using System.ComponentModel;
using System.Threading;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace QingtingRadio.ViewModels
{
    /// <summary>
    /// 播放面板 UI 状态：标题/封面/缓冲指示/面板可见性。
    /// </summary>
    public class RadioPlayPanelViewModel : INotifyPropertyChanged
    {
        private string imageuri;
        private string title;
        private string nowplaying;
        private string playid;
        private bool isreplay;
        private bool isbuffering;
        private Visibility largevisibility = Visibility.Collapsed;
      
        public  Visibility LargeVisibility
        {
            get { return largevisibility; }
            set
            {
                if (largevisibility != value)
                {
                    largevisibility = value;
                    RaisePropertyChanged("LargeVisibility");
                }
            }
        }

        public string ImageUri
        {
            get { return imageuri; }
            set
            {
                if (imageuri != value)
                {
                    imageuri = value;
                    RaisePropertyChanged("ImageUri");
                }
            }
        }

        public string Title
        {
            get { return title; }
            set
            {
                if (title != value)
                {
                    title = value;
                    RaisePropertyChanged("Title");
                }
            }
        }

        public string NowPlaying
        {
            get { return nowplaying; }
            set
            {
                if (nowplaying != value && !string.IsNullOrEmpty(value))
                {
                    nowplaying = value;
                    RaisePropertyChanged("NowPlaying");
                }
            }
        }

        public string PlayID
        {
            get { return playid; }
            set
            {
                if (playid != value)
                {
                    playid = value;
                    RaisePropertyChanged("PlayID");
                }
            }
        }

        public bool IsReplay
        {
            get { return isreplay; }
            set
            {
                if (isreplay != value)
                {
                    isreplay = value;
                    RaisePropertyChanged("IsReplay");
                }
            }
        }

        public bool IsBuffering
        {
            get { return isbuffering; }
            set
            {
                isbuffering = value;
                RaisePropertyChanged("IsBuffering");
            }
        }

        public RadioPlayPanelViewModel()
        {
            imageuri = "ms-appx:///Assets/recommend_defaultbg.png";
        }

        static RadioPlayPanelViewModel instance;

        public static RadioPlayPanelViewModel Instance
        {
            get
            {
                if (instance == null)
                    instance = new RadioPlayPanelViewModel();
               
                return instance;
            }           
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}