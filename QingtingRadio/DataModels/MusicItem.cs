using QingtingRadio.Https;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace QingtingRadio.DataModels
{
    public class MusicItem
    {   
        public string Id { get; set; }
        public string Title { get; set; }
        public string ImageUri { get; set; }
        public string PlayCount { get; set; }
        public AudioType Type { get; set; }
        public enum AudioType
        {
            None,
            Live,
            Replay,
            Program,
            Album,
            Search,
        };

        public MusicItem() { }        

        public MusicItem(JsonObject json, AudioType type):this()
        {
            Type = type;
            switch (type)
            {
                case AudioType.Live:
                    Id = GetJsonNumber(json, "content_id");
                    Title = GetJsonString(json, "title");
                    ImageUri = GetJsonString(json, "cover");
                    PlayCount = GetJsonNumber(json, "audience_count");
                    break;
                case AudioType.Replay:
                    Id = GetJsonNumber(GetJsonObject(json, "mediainfo"), "id");
                    Title = GetJsonString(json, "title");
                    break;
                case AudioType.Program:
                    Id = GetJsonNumber(json, "id");
                    Title = GetJsonString(json, "title");
                    PlayCount = GetJsonString(json, "playcount");
                    break;
                case AudioType.Album:
                    Id = GetJsonNumber(json, "id");
                    Title = GetJsonString(json, "title");                 
                    PlayCount = GetJsonString(json, "playcount");
                    JsonObject thumbs = GetJsonObject(json, "thumbs");
                    ImageUri = GetJsonString(thumbs, "800_thumb");
                    break;
                case AudioType.Search:
                    Id = GetJsonNumber(json, "id");
                    Title = GetJsonString(json, "title");
                    PlayCount = GetJsonString(json, "playcount");
                    ImageUri = GetJsonString(json, "cover");
                    break;
                default:
                    break;
            }
        }

        public virtual MediaPlaybackItem ToPlaybackItem(MediaSource source)
        {
            return new MediaPlaybackItem(source);
        }

        public static string GetJsonString(JsonObject json, string key)
        {
            if (!IsValueValid(json, key))
            {
                return string.Empty;
            }

            return json.GetNamedString(key);
        }

        public static string GetJsonNumber(JsonObject json, string key)
        {
            if (!IsValueValid(json, key))
            {
                return string.Empty;
            }

            return json.GetNamedNumber(key).ToString();
        }

        public static JsonArray GetJsonArray(JsonObject json, string key)
        {
            if (!IsValueValid(json, key))
            {
                return null;
            }

            return json.GetNamedArray(key);
        }

        public static JsonObject GetJsonObject(JsonObject json, string key)
        {
           if(!IsValueValid(json, key))
            {
                return null;
            }

            return json.GetNamedObject(key);
        }

        public static bool IsValueValid(JsonObject json, string key)
        {
            if (json == null)
            {
                return false;
            }
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            if (!json.ContainsKey(key))
            {
                return false;
            }
            if (json[key].ValueType == JsonValueType.Null)
            {
                return false;
            }

            return true;
        }

       public static string GetSign(string content)
       {
            // 签名算法（含密钥）已移入私有程序集 QingtingRadio.Core
            return ApiSigner.GetSign(content);
       } 

       public static string GetProgramPath(string albumId, string mediaId)
       {
            long currentTicks = DateTime.Now.Ticks;
            DateTime dtFrom = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            long currentMillis = (currentTicks - dtFrom.Ticks) / 10000;

            string signStr = string.Format(ServiceURLs.Signature, albumId, mediaId, currentMillis);
            string sign = GetSign(signStr);

            return string.Format(ServiceURLs.ProgramPath, albumId, mediaId, currentMillis, sign);
        }
    }
}
