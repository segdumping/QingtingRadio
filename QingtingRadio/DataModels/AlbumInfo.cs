using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Media.Playback;

namespace QingtingRadio.DataModels
{
    public class AlbumInfo : MusicItem
    {
        public string Description { get; set; }
        public string UpdateTime { get; set; }
        public string SaleType { get; set; }
        public string PragromCount { get; set; }
        public string Version { get; set; }

        public AlbumInfo() { }

        public AlbumInfo(JsonObject json, AudioType type = AudioType.Album) : base(json, type)
        {
            if (type != AudioType.Album)
            {
                return;
            }

            Description = GetJsonString(json, "description");
            UpdateTime  = GetJsonString(json, "update_time");
            SaleType    = GetJsonNumber(json, "view_type");
            PragromCount = GetJsonNumber(json, "program_count");
            Version = GetJsonString(json, "v");
        }
    }
}
