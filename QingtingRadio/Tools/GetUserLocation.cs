using QingtingRadio.DataModels;
using QingtingRadio.Https;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace QingtingRadio.Tools
{
    /// <summary>
    /// 按 IP 定位用户所在地区，并映射到本地区域电台。
    /// </summary>
    public class GetUserLocation
    {
        APIService _api = new APIService();
        public async Task<LocalRadio> GetUserLocationByIp()
        {
            try
            {
                string UserLocation = await _api.GetRegionName();
                if (string.IsNullOrEmpty(UserLocation)) return null;
                LocalRadio tmp = new LocalRadio();
                var storageFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(ServiceURLs.RadioRegions));
                var jsonText = await FileIO.ReadTextAsync(storageFile);
                if (string.IsNullOrEmpty(jsonText)) return null;
                JsonObject json = JsonObject.Parse(jsonText);
                if (json == null) return null;
                var Datas = json["Data"];
                if (Datas == null) return null;

                JsonArray ja = Datas.GetArray();
                foreach (var jsonItem in ja)
                {
                    JsonObject jo = jsonItem.GetObject();
                    string regionName = jo["title"].GetString();
                    if (UserLocation.Contains(regionName) || regionName.Contains(UserLocation))
                    {
                        tmp.RegionName = regionName;
                        tmp.RadioID = jo["id"].GetNumber().ToString();
                        break;
                    }
                }
                if (tmp.RadioID != null)
                {
                    return tmp;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get user location", ex);
                return null;
            }
        }
    }
}