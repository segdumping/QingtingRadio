using QingtingRadio.Https;
using QingtingRadio.ViewModels;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace QingtingRadio.Tools
{
    /// <summary>
    /// 版本更新提示：读取随包的版本日志并弹出更新通知。
    /// </summary>
    public class VersionUpdateHelper
    {
        public async Task<string> VersionLog(bool getLog)
        {
            try
            {
                StorageFile storageFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(ServiceURLs.VersionLog));
                var jsonText = await FileIO.ReadTextAsync(storageFile);
                JsonObject json = JsonObject.Parse(jsonText);
                var Datas = json["versionlog"].GetObject()["logs"];
                if (Datas != null)
                {
                    string tmp = string.Empty;
                    JsonArray ja = Datas.GetArray();
                    string toastedKey = RadioSettingsViewModel.Instance.ToastedKey;
                    foreach (var jsonItem in ja)
                    {

                        JsonObject jo = jsonItem.GetObject();
                        if (getLog)
                        {
                            tmp += string.Format("V{0} （{1}）：\r\n{2}\r\n", jo["version"].GetString(), jo["date"].GetString(), jo["log"].GetString());
                        }
                        else
                        {
                            string key = jo["version"].GetString();
                            if (VersionHelper.Compare(key, _settings.CurrentVersion) == 0)
                            {
                                RadioSettingsViewModel.Instance.ToastedKey = _settings.CurrentVersion;
                                return jo["toast"].GetString();
                            }
                        }
                    }
                    return tmp;
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogManager.LogAsync("version log", ex);

                return null;
            }
        }

        private readonly RadioSettingsViewModel _settings = RadioSettingsViewModel.Instance;

        private VersionUpdateHelper() { }
        public static readonly VersionUpdateHelper Current = new VersionUpdateHelper();
    }
}