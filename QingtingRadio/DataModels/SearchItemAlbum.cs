using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace QingtingRadio.DataModels
{
    /// <summary>
    /// 搜索结果条目（专辑）。
    /// </summary>
    public class SearchItemAlbum : AlbumInfo
    {    
        public SearchItemAlbum() { }

        public SearchItemAlbum(JsonObject json) : base(json, AudioType.Search)
        {
            // v3/search 里的字段是 updatetime（无下划线）
            UpdateTime = GetJsonNumber(json, "updatetime");
            Description = GetJsonString(json, "description");
        }
    }
}