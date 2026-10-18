using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Xaml.Media.Imaging;

using QingtingRadio.DataModels;
using QingtingRadio.Tools;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using static QingtingRadio.Tools.PlayInfoHelper;

namespace QingtingRadio.Https
{
    public class APIService : APIBaseService
    {      
        public async Task<List<Categories>> GetRadioCategories(string Uri)
        {
            if (!NetworkManager.IsNetConnected())
                return null;

            try
            {
                JsonObject json = await GetJson(Uri);
                if (json == null)
                {
                    return null;
                }

                var Datas = json["Data"];
                if (Datas == null)
                {
                    return null;
                }

                List<Categories> CategoryList = new List<Categories>();
                JsonArray ja = Datas.GetArray();
                foreach (var j in ja)
                {
                    JsonObject jo = j.GetObject();
                    Categories tmp = new Categories
                    {
                        ID = MusicItem.GetJsonNumber(jo, "id"),
                        Title = MusicItem.GetJsonString(jo, "title"),
                    };
                    CategoryList.Add(tmp);
                }
                return CategoryList;
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get radio categories " + Uri, ex);
                return null;
            }
        }

        public async Task<List<RadioItem>> GetRadiosList(string Uri, string RadioID = null)
        {
            if (!NetworkManager.IsNetConnected())
                return null;

            try
            {
                JsonObject json = await GetJson(Uri);
                if (json == null)
                {
                    return null;
                }

                List<RadioItem> RadiosList = new List<RadioItem>();
                var Datas = json["Data"];
                if (Datas == null)
                {
                    return null;
                }

                JsonArray ja = Datas.GetArray();
                foreach (var j in ja)
                {
                    JsonObject jo = j.GetObject();
                    RadioItem tmp = new RadioItem(jo);
                    RadiosList.Add(tmp);
                }

                return RadiosList;
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get radios list " + Uri, ex);
                return null;
            }
        }


        public async Task<List<MusicItem>> GetSearchResult(string Uri)
        {
            if (!NetworkManager.IsNetConnected())
                return null;

            try
            {
                JsonObject json = await GetJson(Uri);
                if (json == null) return null;
                JsonArray array = json["data"].GetObject()["data"].GetObject()["docs"].GetArray();
                if (array == null) return null;
                List<MusicItem> searchResults = new List<MusicItem>();

                foreach (var j in array)
                {
                    JsonObject jo = j.GetObject();
                    string type = MusicItem.GetJsonString(jo, "type");
                    if (type.Equals("people_podcaster") || type.Equals("program_live") || type.Equals("program_temp"))
                    {
                        continue;
                    }

                    if (type.Equals("program_ondemand"))
                    {
                        // v3/search 的节目结果里既没有 file_path 也没有 mediainfo_id（旧代码用
                        // file_path 过滤，导致所有节目搜索结果被静默丢弃）。播放地址由
                        // parent_id(频道/专辑) + 节目 id 现算，因此这里只校验这两个必需字段。
                        string programId = MusicItem.GetJsonNumber(jo, "id");
                        string parentId = MusicItem.GetJsonNumber(jo, "parent_id");
                        if (string.IsNullOrEmpty(programId) || string.IsNullOrEmpty(parentId)) continue;

                        SearchItemProgram program = new SearchItemProgram(jo);
                        searchResults.Add(program);
                    }
                    else if (type.Equals("channel_live"))
                    {
                        string mediaId = MusicItem.GetJsonNumber(jo, "mediainfo_id");
                        if (string.IsNullOrEmpty(mediaId)) continue;

                        SearchItemRadio radio = new SearchItemRadio(jo);
                        searchResults.Add(radio);
                    }
                    else if (type.Equals("channel_ondemand"))
                    {
                        SearchItemAlbum album = new SearchItemAlbum(jo);
                        searchResults.Add(album);
                    }
                    else
                    { }
                }


                return searchResults;
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get search result " + Uri, ex);
                return null;
            }
        }

        public async Task<AlbumInfo> GetAlbumInfo(string Uri)
        {
            if (!NetworkManager.IsNetConnected())
                return null;

            try
            {
                JsonObject json = await GetJson(Uri);
                if (json == null) return null;
                JsonObject data = json["data"].GetObject();
                if (data == null) return null;

                return new AlbumInfo(data);
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get album info " + Uri, ex);
                return null;
            }
        }

        public async Task<List<AlbumItem>> GetAlbumList(string Uri, string albumId = null)
        {
            if (!NetworkManager.IsNetConnected())
            {
                return null;
            }

            try
            {
                JsonObject json = await GetJson(Uri);
                if (json == null) return null;
                JsonArray data = json["data"].GetObject()["programs"].GetArray();
                if (data == null) return null;

                List<AlbumItem> albumList = new List<AlbumItem>();
                foreach (var j in data)
                {
                    JsonObject jo = j.GetObject();
                    AlbumItem tmp = new AlbumItem(jo, albumId);
                    albumList.Add(tmp);
                }

                return albumList;
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get album list " + Uri, ex);
                return null;
            }
        }

        public async Task<List<ReplayRadio>> GetReplayList(string Uri, string Date)
        {
            if (!NetworkManager.IsNetConnected())
                return null;

            try
            {
                JsonObject json = await GetJson(Uri);
                if (json == null) return null;

                List<ReplayRadio> ReplayList = new List<ReplayRadio>();
                JsonObject data = json["data"].GetObject();
                if (data == null) return null;

                var value = data.Values.ToArray().ElementAt(0);
                JsonArray ja = value.GetArray();
                foreach (var j in ja)
                {
                    JsonObject jo = j.GetObject();
                    ReplayRadio replay = new ReplayRadio(jo, Date);
                    replay.Id = MusicItem.GetJsonNumber(jo, "channel_id");
                    ReplayList.Add(replay);
                }

                return ReplayList;
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get replay list " + Uri, ex);
                return null;
            }
        }

        public async Task<string> GetMdeiaInfo(string Uri)
        {
            if (!NetworkManager.IsNetConnected())
                return null;

            try
            {
                JsonObject json = await GetJson(Uri);
                if (json == null) return null;

                var Data = json["Data"];
                if (Data.ValueType == JsonValueType.Null) return null;

                JsonObject jo = Data.GetObject();
                return MusicItem.GetJsonNumber(jo, "id");
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get media info " + Uri, ex);
                return null;
            }
        }

        public async Task<List<string>> GetHotKeyWords()
        {
            if (!NetworkManager.IsNetConnected())
                return null;

            try
            {
                JsonObject json = await GetJson(ServiceURLs.HotKeyWords);
                if (json == null) return null;

                JsonArray ja = json["data"].GetArray();
                if (ja == null) return null;

                List<string> keyWords = new List<string>();
                foreach (var j in ja)
                {
                    JsonObject tmp = j.GetObject();
                    if (MusicItem.GetJsonNumber(tmp, "id") != "0")
                    {
                        continue;
                    }

                    var words = MusicItem.GetJsonArray(tmp, "hot_keywords");
                    foreach (var w in words)
                    {
                        keyWords.Add(w.GetString());
                    }
                }

                return keyWords;
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get hot keywords", ex);
                return null;
            }
        }

        public async Task<Dictionary<string, string>> GetLiveChannelPlaying(string Uri)
        {
            if (!NetworkManager.IsNetConnected())
                return null;

            try
            {
                JsonObject json = await GetJson(Uri);
                if (json == null) return null;

                var datas = json["data"];
                if (datas == null) return null;

                JsonArray ja = datas.GetArray();
                Dictionary<string, string> playing = new Dictionary<string, string>();
                foreach (var j in ja)
                {
                    try
                    {
                        JsonObject jo = j.GetObject();
                        JsonObject tmpObj = jo["programs"].GetArray().ElementAt(0).GetObject();

                        string id = MusicItem.GetJsonNumber(jo, "id");
                        string nowPlaying = MusicItem.GetJsonString(tmpObj, "name");

                        playing[id] = nowPlaying;
                    }
                    catch (Exception ex)
                    {
                        // 单条节目解析失败不阻断整表，记录后继续
                        LogManager.LogAsync("get live channel playing item", ex);
                    }
                }
                return playing;
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get live channel playing " + Uri, ex);
                return null;
            }
        }

        public async Task<string> GetRegionName()
        {
            if (!NetworkManager.IsNetConnected())
                return null;

            try
            {
                string json = await GetHtml(ServiceURLs.UserRegion);
                if (string.IsNullOrEmpty(json)) return null;

                int bIndex = json.IndexOf("{", StringComparison.Ordinal);
                int eIndex = json.IndexOf("}", StringComparison.Ordinal) - bIndex + 1;
                if (bIndex < 0 || eIndex <= bIndex) return null;

                JsonObject jo = JsonObject.Parse(json.Substring(bIndex, eIndex));
                return MusicItem.GetJsonString(jo, "cname");
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get region name", ex);
                return null;
            }
        }

        public async Task DownloadImage(string imageUri)
        {
            try
            {
                string[] sArray = imageUri.Split('/');
                string ImageId = sArray[sArray.Length - 1].Split('!')[0];
                if (string.IsNullOrEmpty(ImageId)) return;

                // 保存原始压缩字节，不再解码成 WriteableBitmap 后重编码
                Windows.Storage.Streams.IBuffer buffer = await GetImageBytes(imageUri);
                if (buffer == null) return;

                await FileHelper.Current.SaveImageBytesAsync(buffer.ToArray(), ImageId);
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("download image " + imageUri, ex);
            }
        }
    }
}
