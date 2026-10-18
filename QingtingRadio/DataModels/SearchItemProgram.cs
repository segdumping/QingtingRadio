using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace QingtingRadio.DataModels
{
    /// <summary>
    /// 搜索结果条目（节目）：由搜索接口字段推算播放地址。
    /// </summary>
    public class SearchItemProgram : AlbumItem
    {
        public string MediaId { get; set; }     

        public SearchItemProgram() { }

        public SearchItemProgram(JsonObject json) : base(json, "", AudioType.Search)
        {  
            // 注意：v3/search 返回的字段是 parent_id / updatetime，
            // 且没有 mediainfo_id（原代码取 mediainfo_id 得到空值，导致播放地址失效）。
            AlbumId = GetJsonNumber(json, "parent_id");
            Duration = GetJsonNumber(json, "duration");
            UpdateTime = GetJsonNumber(json, "updatetime");
            // 播放地址：parent_id(频道/专辑) + 节目 id，与专辑列表项的算法保持一致
            MediaId = Id;
            if (string.IsNullOrEmpty(MediaId))
            {
                MediaId = GetJsonNumber(json, "id");
            }
            FilePath = GetProgramPath(AlbumId, MediaId);
            // 接口无 parent_name，专辑名只能从 description（"来自专辑《xxx》"）里取
            AlbumName = GetJsonString(json, "parent_name");
            if (string.IsNullOrEmpty(AlbumName))
            {
                AlbumName = ExtractAlbumName(GetJsonString(json, "description"));
            }
        }

        private static string ExtractAlbumName(string description)
        {
            if (string.IsNullOrEmpty(description)) return string.Empty;

            int start = description.IndexOf('《');
            int end = description.IndexOf('》');
            if (start >= 0 && end > start + 1)
            {
                return description.Substring(start + 1, end - start - 1);
            }

            return string.Empty;
        }
    }
}