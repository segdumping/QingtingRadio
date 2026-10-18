using QingtingRadio.Services;
using QingtingRadio.Tools;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage;
using Windows.UI.ViewManagement;

namespace QingtingRadio.ViewModels
{
    /// <summary>
    /// 应用设置（LocalSettings 读写）：主题、清晰度、地区、定时停播等。
    /// </summary>
    public class RadioSettingsViewModel : INotifyPropertyChanged
    {
        public bool  AppTheme
        {
            get
            {
                return ReadSettings(nameof(AppTheme), false);
            }
            set
            {
                SaveSettings(nameof(AppTheme), value);
                NotifyPropertyChanged(nameof(AppTheme));
            }
        }

        public bool HasCustomRegion
        {
            get
            {
                return ReadSettings(nameof(HasCustomRegion), false);
            }
            set
            {
                SaveSettings(nameof(HasCustomRegion), value);
                NotifyPropertyChanged(nameof(HasCustomRegion));
            }
        }

        public string  UserRegionName
        {
            get
            {
                return ReadSettings(nameof(UserRegionName), "上海台");
            }
            set
            {
                SaveSettings(nameof(UserRegionName), value);
                NotifyPropertyChanged(nameof(UserRegionName));
            }
        }

        public string UserRegionID
        {
            get
            {
                return ReadSettings(nameof(UserRegionID), "83");
            }
            set
            {
                SaveSettings(nameof(UserRegionID), value);
                NotifyPropertyChanged(nameof(UserRegionID));
            }
        }

        public bool MoNet
        {
            get
            {
                return ReadSettings(nameof(MoNet), false);
            }
            set
            {
                SaveSettings(nameof(MoNet), value);
                NotifyPropertyChanged(nameof(MoNet));
            }
        }

        public bool MoNetDownload
        {
            get
            {
                return ReadSettings(nameof(MoNetDownload), false);
            }
            set
            {
                SaveSettings(nameof(MoNetDownload), value);
                NotifyPropertyChanged(nameof(MoNetDownload));
            }
        }

        public bool IsHighBitrate
        {
            get
            {
                return ReadSettings(nameof(IsHighBitrate), false);
            }
            set
            {
                SaveSettings(nameof(IsHighBitrate), value);
                NotifyPropertyChanged(nameof(IsHighBitrate));
            }
        }

        public int Bitrate
        {
            get
            {
                bool bitRate = ReadSettings(nameof(IsHighBitrate), false);
                return bitRate ? 1000 : 0;
            }            
        }

        public bool TransparentTile
        {
            get
            {
                return ReadSettings(nameof(TransparentTile), false);
            }
            set
            {
                SaveSettings(nameof(TransparentTile), value);
                NotifyPropertyChanged(nameof(TransparentTile));
            }
        }

        public bool AutoSwitchTheme
        {
            get
            {
                return ReadSettings(nameof(AutoSwitchTheme), false);
            }
            set
            {
                SaveSettings(nameof(AutoSwitchTheme), value);
                NotifyPropertyChanged(nameof(AutoSwitchTheme));
            }
        }

        public double AutoSwitchThemeStartTime
        {
            get
            {
                return ReadSettings(nameof(AutoSwitchThemeStartTime), 0.0);
            }
            set
            {
                SaveSettings(nameof(AutoSwitchThemeStartTime), value);
                NotifyPropertyChanged(nameof(AutoSwitchThemeStartTime));
            }
        }

        public double AutoSwitchThemeEndTime
        {
            get
            {
                return ReadSettings(nameof(AutoSwitchThemeEndTime), 0.0);
            }
            set
            {
                SaveSettings(nameof(AutoSwitchThemeEndTime), value);
                NotifyPropertyChanged(nameof(AutoSwitchThemeEndTime));
            }
        }

        public bool TimerPause
        {
            get
            {
                return ReadSettings(nameof(TimerPause), false);
            }
            set
            {
                SaveSettings(nameof(TimerPause), value);
                NotifyPropertyChanged(nameof(TimerPause));
            }
        }

        public double TimerPauseTime
        {
            get
            {
                return ReadSettings(nameof(TimerPauseTime), 0.0);
            }
            set
            {
                SaveSettings(nameof(TimerPauseTime), value);
                NotifyPropertyChanged(nameof(TimerPauseTime));
            }
        }

        public double CacheSize
        {
            get
            {
                return ReadSettings<double>(nameof(CacheSize), 0);
            }
            set
            {
                SaveSettings(nameof(CacheSize), value);
                NotifyPropertyChanged(nameof(CacheSize));
            }
        }

        public string ToastedKey
        {
            get
            {
                string key = ReadSettings(nameof(ToastedKey), "1.1.10");
                return VersionHelper.Compare(key, "1.1.10") > 0 ? key : "1.1.10";
            }
            set
            {
                SaveSettings(nameof(ToastedKey), value);
            }
        }

        public long TimeTamp
        {
            get
            {
                long epoch = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000;
                long time = ReadSettings<long>(nameof(TimeTamp), 0);
                if(0 == time)
                {
                    SaveSettings(nameof(TimeTamp), epoch);
                    return epoch;
                }

                return time;             
            }
            set
            {
                SaveSettings(nameof(TimeTamp), value);
                NotifyPropertyChanged(nameof(TimeTamp));
            }
        }

        public double Volume
        {
            get
            {
                return ReadSettings<double>(nameof(Volume), 100);
            }
            set
            {
                SaveSettings(nameof(Volume), value);
                PlaybackService.Instance.Player.Volume = value /100;
                NotifyPropertyChanged(nameof(Volume));
            }
        }

        public string CurrentVersion
        {
            get
            {
                var v = Package.Current.Id.Version;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
        }

        public string DeviceId
        {
            get
            {
                string tmp = ReadSettings<string>(nameof(DeviceId), null);
                if (string.IsNullOrEmpty(tmp))
                {
                    EasClientDeviceInformation deviceInfo = new EasClientDeviceInformation();
                    tmp = deviceInfo.Id.ToString();
                    SaveSettings(nameof(DeviceId), tmp);
                }

                return tmp;
            }
        }
        
        public ApplicationDataContainer LocalSettings { get; set; }
        private RadioSettingsViewModel()
        {
            LocalSettings = ApplicationData.Current.LocalSettings;
        }

        private void SaveSettings(string key, object value)
        {
            LocalSettings.Values[key] = value;
        }

        private T ReadSettings<T>(string key, T defaultValue)
        {
            if (LocalSettings.Values.ContainsKey(key))
            {
                return (T)LocalSettings.Values[key];
            }
            if (null != defaultValue)
            {
                return defaultValue;
            }
            return default(T);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged([CallerMemberName]string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public static readonly RadioSettingsViewModel Instance = new RadioSettingsViewModel();
    }
}