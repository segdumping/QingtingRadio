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
    public class AlbumItem : MusicItem
    {
        public string Duration { get; set; }
        public string FilePath { get; set; }
        public string UpdateTime { get; set; }
        public string AlbumName { get; set; }
        public string AlbumId { get; set; }

        public AlbumItem() { }

        public AlbumItem(JsonObject json, string albumId, AudioType type = AudioType.Program) : base(json, type)
        {
            if (type != AudioType.Program)
            {
                return;
            }

            AlbumId = albumId;
            UpdateTime = GetJsonString(json, "update_time");
            Duration = GetJsonNumber(json, "duration");
            FilePath = GetProgramPath(AlbumId, Id);
        }

        public override MediaPlaybackItem ToPlaybackItem(MediaSource source)
        {
            var playbackItem =  base.ToPlaybackItem(source);

            var displayProperties = playbackItem.GetDisplayProperties();
            displayProperties.Type = MediaPlaybackType.Music;
            displayProperties.MusicProperties.Title = string.IsNullOrEmpty(Title) ? "未知" : Title;
            displayProperties.MusicProperties.Artist = string.IsNullOrEmpty(AlbumName) ? "未知" : AlbumName;
            playbackItem.ApplyDisplayProperties(displayProperties);

            return playbackItem;
        }
    }
}
