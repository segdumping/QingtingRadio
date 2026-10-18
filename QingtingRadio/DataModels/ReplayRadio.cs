using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using QingtingRadio.Tools;

namespace QingtingRadio.DataModels
{
    /// <summary>
    /// 电台回听节目条目：起止时间与日期，支持收藏持久化。
    /// </summary>
    public class ReplayRadio : MusicItem
    {
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string DateTime { get; set; }
        public string Duration { get; set; }
        public string RadioTitle { get; set; }

        public ReplayRadio() { }

        public ReplayRadio(JsonObject json, string date) : base(json, AudioType.Replay)
        {
            StartTime = GetJsonString(json, "start_time");
            EndTime = GetJsonString(json, "end_time");
            Duration = GetJsonNumber(json, "duration");

            DateTime = date + " " + StartTime.Substring(0, 5) + "-" + EndTime.Substring(0, 5);
        }

        public override MediaPlaybackItem ToPlaybackItem(MediaSource source)
        {
            var playbackItem = base.ToPlaybackItem(source);

            var displayProperties = playbackItem.GetDisplayProperties();
            displayProperties.Type = MediaPlaybackType.Music;
            displayProperties.MusicProperties.Title = string.IsNullOrEmpty(Title) ? "未知" : Title;
            displayProperties.MusicProperties.Artist = string.IsNullOrEmpty(RadioTitle) ? "未知" : RadioTitle;
            playbackItem.ApplyDisplayProperties(displayProperties);

            return playbackItem;
        }

        #region 收藏持久化
        // 回听节目收藏统一存取，文件名只在此处出现
        private const string FavoriteStoreFile = "FavoriteReplays.json";

        public static Task<List<ReplayRadio>> LoadFavoritesAsync()
        {
            return FileHelper.Current.ReadObjectAsync<List<ReplayRadio>>(FavoriteStoreFile);
        }

        public static Task SaveFavoritesAsync(List<ReplayRadio> list)
        {
            return FileHelper.Current.WriteObjectAsync(list, FavoriteStoreFile);
        }
        #endregion
    }
}