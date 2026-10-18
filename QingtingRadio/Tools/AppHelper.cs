using QingtingRadio.DataModels;
using QingtingRadio.Services;
using QingtingRadio.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;
using Windows.UI.Popups;
using Microsoft.Services.Store.Engagement;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System.Profile;
using Windows.ApplicationModel;

namespace QingtingRadio.Tools
{
    /// <summary>
    /// 应用级助手：前后台切换处理、设备信息、定时停播、网络检查等杂项。
    /// </summary>
    public class AppHelper
    {
        private Timer _autoStopTimer;
        private RadioSettingsViewModel _settings = RadioSettingsViewModel.Instance;

        public void AppEnteredHandle()
        {
            try
            {
                _ = SavePlayPosition();
            }
            catch(Exception ex)
            {
                LogManager.LogAsync("app entered handler", ex);
            }
        }

        public async Task AppLeavingHandle()
        {
            try
            {
                await CheckReview();
            }
            catch
            { }
        }

        public static async Task RegisterToast()
        {
            try
            {
                StoreServicesEngagementManager engagementManager = StoreServicesEngagementManager.GetDefault();
                await engagementManager.RegisterNotificationChannelAsync();
            }
            catch(Exception ex)
            {
                LogManager.LogAsync("register toast", ex);
            }
        }

        private async Task CheckReview()
        {
            long epoch = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000;
            if (_settings.TimeTamp == -1) return;
            if(epoch - _settings.TimeTamp > 86400 * 7)
            {
                MessageDialog message = new MessageDialog("你已经使用蜻蜓电台一段时间了，给个好评吧。");
                message.Commands.Add(new UICommand("给个好评", cmd => { }, commandId: 0));
                message.Commands.Add(new UICommand("还没想好", cmd => { }, commandId: 1));

                message.DefaultCommandIndex = 0;
                message.CancelCommandIndex = 1;

                var ret = await message.ShowAsync();
                if (ret.Label.Equals("给个好评"))
                {
                    bool result = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9nf53rwzbswz"));
                    if(result)
                    {
                        _settings.TimeTamp = -1;
                    }             
                }
                else
                {
                    _settings.TimeTamp = epoch;
                }             
            }
        }

        public async Task SavePlayPosition()
        {
            try
            {
                LastestPlay item = await LastestPlay.LoadCurrentAsync();
                if (item != null)
                {
                    item.Position = PlaybackService.Instance.Player.PlaybackSession.Position.TotalSeconds;
                    item.TotalSeconds = PlaybackService.Instance.Player.PlaybackSession.NaturalDuration.TotalSeconds;

                    await LastestPlay.SaveCurrentAsync(item);

                    await RadioPlayViewModel.Instance.UpdatePlayPosition(item.Id);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("save play position", ex);
            }
        }

#region 网络检查并通知用户
        public void CheckNetwork(string message)
        {
            if (NetworkManager.Current.Network == 5)
            {
                NotifyUser notifyPopup = new NotifyUser(message);
                notifyPopup.Show();
            }
        }

        public void ShowOperationMessage(string message)
        {
            NotifyUser notifyPopup = new NotifyUser(message);
            notifyPopup.Show();
        }

        public static async Task<bool> ShowMessageDialog(string message, string defaultCommand, string cancelCommand)
        {
            MessageDialog messageDialog = new MessageDialog(message);
            if (!string.IsNullOrEmpty(defaultCommand))
            {
                messageDialog.Commands.Add(new UICommand(defaultCommand, cmd => { }, commandId: 0));
            }

            if (!string.IsNullOrEmpty(cancelCommand))
            {
                messageDialog.Commands.Add(new UICommand(cancelCommand, cmd => { }, commandId: 1));
            }

            var ret = await messageDialog.ShowAsync();
            if (string.IsNullOrEmpty(defaultCommand) || string.IsNullOrEmpty(cancelCommand))
            {
                return true;
            }

            if (ret.Label.Equals(defaultCommand))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool NotifyNetState()
        {
            int network = NetworkManager.Current.Network;
            if (network == 3) // wifi
            {
                return true;
            }
            else if (network == 4) //未知
            {
                return true;
            }
            else if (network == 5) //无连接
            {
                NotifyUser notifyPopup = new NotifyUser("打开网络连接才能收听哦");
                notifyPopup.Show();
                return false;
            }
            else //234G
            {
                if (RadioSettingsViewModel.Instance.MoNet)
                {
                    NotifyUser notifyPopup = new NotifyUser("正在" + NetworkManager.Current.NetworkTitle + "网络收听");
                    notifyPopup.Show();
                    return true;
                }
                else
                {
                    NotifyUser notifyPopup = new NotifyUser("已禁用2G/3G/4G网络播放");
                    notifyPopup.Show();
                    return false;
                }
            }
        }
#endregion

#region 定时关闭播放 

        public void UpdateAutoStopTimer()
        {
            if ((_settings.TimerPause))
            {
                StartAutoStopTimer();
            }
            else
            {
                StopAutoStopTimer();
            }
        }

        private void StartAutoStopTimer()
        {

            if (_autoStopTimer == null)
            {
                _autoStopTimer = new Timer(new TimerCallback(AutoStopTimedOut), null, -1, -1);
            }
            double time = (double)_settings.TimerPauseTime;
            double now = DateTime.Now.TimeOfDay.TotalSeconds;
            // 设定时刻已过则推迟到明天同一时刻触发，否则定时停播当天永远不会生效
            double delay = time > now ? time - now : time + 86400 - now;
            _autoStopTimer.Change(Convert.ToInt32(delay) * 1000, -1);
        }

        private void StopAutoStopTimer()
        {
            try
            {
                if (_autoStopTimer != null)
                {
                    _autoStopTimer.Change(-1, -1);
                }
            }
            finally
            {

            }
        }

        private void AutoStopTimedOut(object state)
        {
            try
            {
                PlaybackService.Instance.Player.Pause();
                PlaybackService.Instance.Player.SystemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Paused;
            }
            finally
            {
                StopAutoStopTimer();
            }
        }
        #endregion

#region 设备信息
        public static string DeviceInfo()
        {
            string info = "{0}\n系统类型：{1}\n系统版本：{2}\n系统架构：{3}\n设备名称：{4}\n设备制造商：{5}\n{6}\n";
                      
            string sv = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong v = ulong.Parse(sv);
            ulong v1 = (v & 0xFFFF000000000000L) >> 48;
            ulong v2 = (v & 0x0000FFFF00000000L) >> 32;
            ulong v3 = (v & 0x00000000FFFF0000L) >> 16;
            ulong v4 = (v & 0x000000000000FFFFL);
            string SystemVersion = $"{v1}.{v2}.{v3}.{v4}";

            Package package = Package.Current;
            string SystemArchitecture = package.Id.Architecture.ToString();

            AnalyticsVersionInfo ai = AnalyticsInfo.VersionInfo;
            EasClientDeviceInformation eas = new EasClientDeviceInformation();

            return string.Format(info,
                "--------------------",
                ai.DeviceFamily,
                SystemVersion,
                SystemArchitecture,
                eas.SystemProductName,
                eas.SystemManufacturer,
                "--------------------");
        }
        #endregion

#region 收藏
        public async Task SaveFavoriteRadio(RadioItem item)
        {
            List<RadioItem> tmpItem = await FileHelper.Current.ReadObjectAsync<List<RadioItem>>("FavoriteRadios.json");
            if (tmpItem == null)
            {
                tmpItem = new List<RadioItem>();
            }

            int index = tmpItem.FindIndex(v1 => v1.Id == item.Id);
            if (index < 0)
            {
                tmpItem.Add(item);
            }

            await FileHelper.Current.WriteObjectAsync<List<RadioItem>>(tmpItem, "FavoriteRadios.json");
            AppHelper.Current.ShowOperationMessage("【" + item.Title + "】" + "收藏成功");
        }

        public async Task SaveFavoriteAlbum(string id)
        {
            AlbumInfo item = await PlayInfoHelper.Current.GetAlbumInfo(id);
            if (item == null) return;
            List<AlbumInfo> tmpItem = await FileHelper.Current.ReadObjectAsync<List<AlbumInfo>>("FavoriteAlbums.json");
            if (tmpItem == null)
            {
                tmpItem = new List<AlbumInfo>();
            }

            int index = tmpItem.FindIndex(v1 => v1.Id == item.Id);
            if (index < 0)
            {
                tmpItem.Add(item);
            }

            await FileHelper.Current.WriteObjectAsync<List<AlbumInfo>>(tmpItem, "FavoriteAlbums.json");
            AppHelper.Current.ShowOperationMessage("【" + item.Title + "】" + "收藏成功");
        }

        public async Task SaveFavoriteProgram(AlbumItem item)
        {
            List<AlbumItem> tmpItem = await FileHelper.Current.ReadObjectAsync<List<AlbumItem>>("FavoritePrograms.json");
            if (tmpItem == null)
            {
                tmpItem = new List<AlbumItem>();
            }

            int index = tmpItem.FindIndex(v1 => v1.Id == item.Id);
            if (index < 0)
            {
                tmpItem.Add(item);
            }

            await FileHelper.Current.WriteObjectAsync<List<AlbumItem>>(tmpItem, "FavoritePrograms.json");
            AppHelper.Current.ShowOperationMessage("【" + item.Title + "】" + "收藏成功");
        }

        #endregion
        public static double GetWidth(double width, int max, int min, int offset = 16)
        {
            int column = (int)width / min;
            double w = (width - column * offset * 2) / column;

            return w - offset;
        }

        private AppHelper() { }
        public static readonly AppHelper Current = new AppHelper();
    }
}