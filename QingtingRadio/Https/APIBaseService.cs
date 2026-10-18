using QingtingRadio.Tools;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage.Streams;

namespace QingtingRadio.Https
{
    /// <summary>
    /// api服务基类
    /// </summary>
    public class APIBaseService
    {
        protected async Task<JsonObject> GetJson(string url)
        {
            string json = await BaseService.SendGetRequest(url);
            if (json != null)
            {
                return JsonObject.Parse(json);
            }
            else
            {
                return null;
            }
        }
        protected async Task<string> GetHtml(string url)
        {
            return await BaseService.SendGetRequest(url);
        }

        // 拉取图片
        protected async Task<IBuffer> GetImageBytes(string url)
        {
            try
            {
                return await BaseService.SendGetRequestAsBytes(url);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("get image " + url, ex);

                return null;
            }
        }
    }
}
