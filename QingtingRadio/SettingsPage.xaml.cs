using Microsoft.Toolkit.Uwp.UI.Extensions;
using QingtingRadio.DataModels;
using QingtingRadio.Tools;
using QingtingRadio.ViewModels;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Contacts;
using Windows.ApplicationModel.Email;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Profile;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace QingtingRadio
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isLoadingSettings = false;
        public SettingsPage()
        {
            this.InitializeComponent();

            this.Loaded += SettingsPage_Loaded;
            this.Unloaded += SettingsPage_Unloaded;
            NavigationCacheMode = NavigationCacheMode.Required;

            _isLoadingSettings = true;
            this.DataContext = RadioSettingsViewModel.Instance;
            SetComoBoxSource();              
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoadingSettings = false;
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
      
            GetCacheSize();
        }

        private async void SetComoBoxSource()
        {
            List<Categories> list = await PlayInfoHelper.Current.RegionInfo();
            if(list != null)
            {
                Region.ItemsSource = list;

                int index = list.FindIndex(v => v.ID == RadioSettingsViewModel.Instance.UserRegionID);
                Region.SelectedIndex = index < 0 ? 0 : index;
            }
        }

        private async void TransparentTileSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            bool isLoadingSettings = _isLoadingSettings;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (TransparentTileSwitch.IsOn && !SecondaryTile.Exists("qingtingradio") && !isLoadingSettings)
                {
                    var tile = new SecondaryTile();
                   
                    tile.VisualElements.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
                
                    tile.DisplayName = "蜻蜓电台";
                    
                    tile.VisualElements.Square71x71Logo = new Uri("ms-appx:///Assets/Square142x142LogoTransport.scale.png");
                    tile.VisualElements.Square150x150Logo = new Uri("ms-appx:///Assets/Square300x300LogoTransport.scale.png");
                    tile.VisualElements.Wide310x150Logo = new Uri("ms-appx:///Assets/Square620x300LogoTransport.scale.png");
                    tile.VisualElements.Square310x310Logo = new Uri("ms-appx:///Assets/Square620x620LogoTransport.scale.png");
                    tile.TileId = "qingtingradio";
                    tile.Arguments = DateTime.Now.ToLocalTime().ToString();
                    tile.VisualElements.ShowNameOnSquare150x150Logo = true;
                    tile.VisualElements.ShowNameOnWide310x150Logo = true;
                    tile.VisualElements.ShowNameOnSquare310x310Logo = true;
                    bool isCreated = await tile.RequestCreateAsync();
                    if(isCreated)
                    {
                        NotifyUser notifyPopup = new NotifyUser("成功固定透明磁贴到屏幕");
                        notifyPopup.Show();
                    }
                }
                else if(!TransparentTileSwitch.IsOn && SecondaryTile.Exists("qingtingradio") && !isLoadingSettings)
                {
                    SecondaryTile tile = new SecondaryTile("qingtingradio");
                    bool isDeleted = await tile.RequestDeleteAsync();
                    if (isDeleted)
                    {
                        NotifyUser notifyPopup = new NotifyUser("成功取消固定透明磁贴");
                        notifyPopup.Show();
                    }
                }
                else
                { }
            });
        }

        private async void MobileNetSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            bool isLoadingSettings = _isLoadingSettings;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (MobileNetSwitch.IsOn && !isLoadingSettings)
                {
                    NotifyUser notifyPopup = new NotifyUser("开启2G/3G/4G播放可能会消耗你的宝贵流量哦");
                    notifyPopup.Show();
                }
            });
        }

        private async void GetCacheSize()
        {
            double size = await FileHelper.Current.GetCacheSize();
            RadioSettingsViewModel.Instance.CacheSize = Math.Round(size / 1024 / 1024 , 2);
        }

        private async void ClearImageCache()
        {
            MessageDialog message = new MessageDialog("确定要清空所有缓存内容吗？");
            message.Commands.Add(new UICommand("确定", cmd => { }, commandId: 0));
            message.Commands.Add(new UICommand("取消", cmd => { }, commandId: 1));

            message.DefaultCommandIndex = 0;
            message.CancelCommandIndex = 1;

            var ret = await message.ShowAsync();
            if(ret.Label.Equals("确定"))
            {
                double tmp = RadioSettingsViewModel.Instance.CacheSize;
                bool result = await FileHelper.Current.DeleteCacheFile();
                if (result)
                {
                    RadioSettingsViewModel.Instance.CacheSize = 0;
                    NotifyUser notifyPopup = new NotifyUser("成功清除缓存：" + tmp + "M");
                    notifyPopup.Show();
                }
            }
        }

        private async void MailFeedBack()
        {
            try
            {
                var emailMessage = new EmailMessage()
                {
                    Subject = "蜻蜓电台意见反馈",
                    Body = "\n\n\n" + AppHelper.DeviceInfo()
                };
             
                {
                    StorageFolder folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("DatasCache", CreationCollisionOption.OpenIfExists);
                    if (folder != null)
                    {
                        StorageFile tmpFile = await folder.GetFileAsync("QingtingLog.txt");
                        if (tmpFile != null)
                        {
                            var stream = RandomAccessStreamReference.CreateFromFile(tmpFile);
                            var attachment = new EmailAttachment(
                                tmpFile.Name,
                                stream);

                            emailMessage.Attachments.Add(attachment);
                        }
                    }
                }
                await EmailManager.ShowComposeNewEmailAsync(emailMessage);
            }
            catch(Exception ex)
            {
                LogManager.LogAsync("mail feedback", ex);
            }
        }

        private async void MobileNetDownload_Toggled(object sender, RoutedEventArgs e)
        {
            bool isLoadingSettings = _isLoadingSettings;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (MobileNetDownload.IsOn && !isLoadingSettings)
                {
                    NotifyUser notifyPopup = new NotifyUser("开启2G/3G/4G下载可能会消耗你的宝贵流量哦");
                    notifyPopup.Show();
                }
            });
        }

        private void AutoThemeSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (AutoThemeSwitch.IsOn)
            {
                EndTime.Time = TimeSpan.FromSeconds(RadioSettingsViewModel.Instance.AutoSwitchThemeEndTime);
                StartTime.Time = TimeSpan.FromSeconds(RadioSettingsViewModel.Instance.AutoSwitchThemeStartTime);
                timePicker.Visibility = Visibility.Visible;
                ThemeSwitch.IsEnabled = false;
            }
            else
            {
                timePicker.Visibility = Visibility.Collapsed;
                ThemeSwitch.IsEnabled = true;
            }
        }

        private void TimerPause_Toggled(object sender, RoutedEventArgs e)
        {
            if (TimerPause.IsOn)
            {
                timerPicker.Time = TimeSpan.FromSeconds(RadioSettingsViewModel.Instance.TimerPauseTime);
                timerPausedTime.Visibility = Visibility.Visible;
            }
            else
            {
                timerPausedTime.Visibility = Visibility.Collapsed;
            }
            AppHelper.Current.UpdateAutoStopTimer();
        }

        private void EndTime_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            RadioSettingsViewModel.Instance.AutoSwitchThemeEndTime = e.NewTime.TotalSeconds;
        }

        private void StartTime_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            RadioSettingsViewModel.Instance.AutoSwitchThemeStartTime = e.NewTime.TotalSeconds;
        }

        private void timerPicker_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            RadioSettingsViewModel.Instance.TimerPauseTime = e.NewTime.TotalSeconds;
            AppHelper.Current.UpdateAutoStopTimer();
        }

        private void Region_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if((sender as ComboBox).SelectedItem is Categories tmp)
            {
                RadioSettingsViewModel.Instance.UserRegionName = tmp.Title + "台";
                RadioSettingsViewModel.Instance.UserRegionID = tmp.ID;
            }       
        }

        private void CustomRegion_Toggled(object sender, RoutedEventArgs e)
        {
            if (CustomRegion.IsOn)
            {
                customRegion.Visibility = Visibility.Visible;
                SetComoBoxSource();
            }
            else
            {
                customRegion.Visibility = Visibility.Collapsed;
            }
        }

        private async void ShowVersionLog()
        {
            string log = await VersionUpdateHelper.Current.VersionLog(true);
            if (string.IsNullOrEmpty(log)) return;

            TextBlock text = new TextBlock()
            {
                Text = log,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                FontSize = 14,
                Margin = new Thickness(12, 8, 0, 4),
            };
                      
            Style customStyle = (Style)Application.Current.Resources["CustomContent"];
            ContentDialog content_dialog = new ContentDialog()
            {
                Content = text,
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = false,            
                FullSizeDesired = true,
                Style = customStyle,
            };

            content_dialog.SecondaryButtonClick += (_s, _e) => { };
            content_dialog.Loaded += (_s, _e) =>
            {
                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                {
                    StatusBar.SetForegroundColor(this, Colors.SlateGray);
                }
            };

            content_dialog.Unloaded += (_s, _e) =>
            {
                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                {
                    Color ground = RadioSettingsViewModel.Instance.AppTheme ? Color.FromArgb(255, 177, 177, 177) : Color.FromArgb(255, 77, 77, 77);
                    StatusBar.SetForegroundColor(this, ground);
                }
            };

            
            content_dialog.RequestedTheme = RadioSettingsViewModel.Instance.AppTheme ? ElementTheme.Dark : ElementTheme.Light;

            await content_dialog.ShowAsync();
        }

        private async void ExportLogFile()
        {
            string msg = string.Empty;

            FileSavePicker savePicker = new FileSavePicker()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "蜻蜓电台日志" + "-" + DateTime.Now.ToString("yyyyMMddHHmmss")
            };
            savePicker.FileTypeChoices.Add("文本文件", new List<string>() { ".txt" });
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file == null) return;

            try
            {
                StorageFolder folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("DatasCache", CreationCollisionOption.OpenIfExists);
                if (folder == null) return;
                StorageFile tmpFile = await folder.GetFileAsync("QingtingLog.txt");
                if (tmpFile == null) return;

                await tmpFile.CopyAndReplaceAsync(file);
                msg = "导出成功";
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("export log file", ex);
                msg = "导出失败";
            }
            finally
            {
                NotifyUser notifyPopup = new NotifyUser(msg);
                notifyPopup.Show();
            }
        }

        private void ToggleList_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {          
            int selectedIndex = (sender as ListBox).SelectedIndex;
            switch (selectedIndex)
            {
                case 0:
                    MobileNetSwitch.IsOn = !MobileNetSwitch.IsOn;
                    break;
                case 1:
                    MobileNetDownload.IsOn = !MobileNetDownload.IsOn;
                    break;
                case 2:
                    HighBitrate.IsOn = !HighBitrate.IsOn;
                    break;
                case 3:
                    TransparentTileSwitch.IsOn = !TransparentTileSwitch.IsOn;
                    break;
                case 4:
                    ThemeSwitch.IsOn = !ThemeSwitch.IsOn;
                    break;
                case 5:
                    AutoThemeSwitch.IsOn = true;
                    break;
                case 6:
                    TimerPause.IsOn = true;
                    break;
                case 7:
                    CustomRegion.IsOn = true;
                    break;
                default:
                    break;
            }
            ToggleList.SelectedIndex = -1;
        }

        private async void SingleList_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {          
            int selectedIndex = (sender as ListBox).SelectedIndex;
            switch (selectedIndex)
            {
                case 0:
                    ClearImageCache();
                    break;
                case 1:
                    await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9nf53rwzbswz"));
                    break;
                case 2:
                    MailFeedBack();
                    break;
                case 3:
                    ShowVersionLog();
                    break;
                case 4:
                    ExportLogFile();
                    break;
                case 5:
                    break;
                default:
                    break;
            }
            SingleList.SelectedIndex = -1;
        }
    }
}
