using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace QingtingRadio.DataModels
{
    /// <summary>
    /// 搜索结果条目（电台）：含 FM 频道号。
    /// </summary>
    public class SearchItemRadio : MusicItem
    {
        public string MeidaId { get; set; }
        public string freqs { get; set; }

        public string Freqs
        {
            get
            {
                if (freqs.Length <= 16)
                {
                    return freqs;
                }
                else
                {
                    return freqs.Substring(0, 16) + "...";
                }
            }
            set
            {
                freqs = value;
            }
        }

        public SearchItemRadio() { }

        public SearchItemRadio(JsonObject json) : base(json, AudioType.Search)
        {
            MeidaId = GetJsonNumber(json, "mediainfo_id");
            Freqs = GetJsonString(json, "freqs");
            if (!string.IsNullOrEmpty(Freqs))
            {
                Freqs = "FM" + Freqs;
            }
        }       
    }
}