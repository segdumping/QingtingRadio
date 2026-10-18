using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using QingtingRadio.Tools;

namespace QingtingRadio.DataModels
{
    /// <summary>
    /// 上次播放快照/收听历史条目：含播放进度，提供统一的持久化读写。
    /// </summary>
    public class LastestPlay  : MusicItem
    {
        public string AlbumId { get; set; }
        public string Playing { get; set; }
        public string PlayUri { get; set; }
        public string FileName { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Date { get; set; }
        public double Position { get; set; }
        public double TotalSeconds { get; set; }

        public LastestPlay() { }

        public override MediaPlaybackItem ToPlaybackItem(MediaSource source)
        {
            var playbackItem = base.ToPlaybackItem(source);

            var displayProperties = playbackItem.GetDisplayProperties();
            displayProperties.Type = MediaPlaybackType.Music;
            displayProperties.MusicProperties.Title = string.IsNullOrEmpty(Playing) ? "未知" : Playing;
            displayProperties.MusicProperties.Artist = string.IsNullOrEmpty(Title) ? "未知" : Title;
            playbackItem.ApplyDisplayProperties(displayProperties);

            return playbackItem;
        }

        #region 本地持久化
        // "上次播放"快照与"收听历史"两个存储文件全项目只在此处出现文件名，
        // 各处一律通过下面四个方法读写，后续要做内存缓存或格式迁移只改这一处。
        private const string CurrentStoreFile = "LastestPlayRadio.json";
        private const string HistoryStoreFile = "HistoryPlayRadio.json";

        public static Task<LastestPlay> LoadCurrentAsync()
        {
            return FileHelper.Current.ReadObjectAsync<LastestPlay>(CurrentStoreFile);
        }

        public static Task SaveCurrentAsync(LastestPlay item)
        {
            return FileHelper.Current.WriteObjectAsync(item, CurrentStoreFile);
        }

        public static Task<List<LastestPlay>> LoadHistoryAsync()
        {
            return FileHelper.Current.ReadObjectAsync<List<LastestPlay>>(HistoryStoreFile);
        }

        public static Task SaveHistoryAsync(List<LastestPlay> list)
        {
            return FileHelper.Current.WriteObjectAsync(list, HistoryStoreFile);
        }
        #endregion
    }
}