using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace QingtingRadio.DataModels
{
    public class RadioItem : MusicItem
    {
        public string NowPlaying { get; set; }
        public string TimeInterval { get; set; }

        public RadioItem() { }

        public RadioItem(JsonObject json) : base(json, AudioType.Live)
        {
            JsonObject tmpObject = GetJsonObject(json, "nowplaying");
            if (tmpObject == null) return;
            NowPlaying = GetJsonString(tmpObject, "title");
            string startTime = GetJsonString(tmpObject, "start_time");
            string duration = GetJsonNumber(tmpObject, "duration");

            //转换时间格式
            if (!string.IsNullOrEmpty(startTime) && !string.IsNullOrEmpty(duration))
            {
                var date = DateTime.Parse(startTime);
                var newdate = date.Add(TimeSpan.FromSeconds(double.Parse(duration)));
                TimeInterval = string.Format("{0}-{1}", date.ToString("t"), newdate.ToString("t"));
            }
        }

        public override MediaPlaybackItem ToPlaybackItem(MediaSource source)
        {
            var playbackItem = base.ToPlaybackItem(source);

            var displayProperties = playbackItem.GetDisplayProperties();
            displayProperties.Type = MediaPlaybackType.Music;
            displayProperties.MusicProperties.Title = string.IsNullOrEmpty(NowPlaying) ? "未知" : NowPlaying;
            displayProperties.MusicProperties.Artist = string.IsNullOrEmpty(Title) ? "未知" : Title;
            playbackItem.ApplyDisplayProperties(displayProperties);

            return playbackItem;
        }
    }
}
