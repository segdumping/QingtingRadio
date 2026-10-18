using QingtingRadio.DataModels;
using QingtingRadio.Https;
using QingtingRadio.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QingtingRadio.Tools
{
    /// <summary>
    /// 节目单/回听助手：日期页签计算、星期映射、地区列表查询。
    /// </summary>
    public class PlayInfoHelper
    {
        APIService _api = new APIService();

        public async Task<List<ReplayRadio>> CurrentPlaybill(string RadioId)
        {
            try
            {
                int DayOfWeek = (Convert.ToInt32(DateTime.Now.DayOfWeek) + 1);
            
                string Uri = string.Format(ServiceURLs.ReplayList, RadioId, DayOfWeek);
                List<ReplayRadio> list = await _api.GetReplayList(Uri, DateTime.Now.ToString("yyyy-MM-dd"));

                return list;
            }
            catch(Exception ex)
            {
                LogManager.LogAsync("current play bill", ex);
                return null;
            }            
        }

        public async Task<ReplayRadio> CurrentPlaying(string RadioId)
        {
            try
            {
                List<ReplayRadio> list = await CurrentPlaybill(RadioId);

                if (list != null)
                {
                    foreach (var l in list)
                    {
                        l.EndTime = l.EndTime.Equals("00:00:00") ? "23:59:59" : l.EndTime;
                        DateTime startDate = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + l.StartTime);                                             
                        DateTime endDate = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " " + l.EndTime);
                        if (l.Duration.Equals("0"))
                        {
                            l.Duration = (endDate - startDate).TotalSeconds.ToString();
                        }

                        if (DateTime.Compare(startDate, DateTime.Now) < 0 && DateTime.Compare(endDate, DateTime.Now) > 0)
                        {
                            return l;
                        }
                    }
                }
                else
                {
                    return null;
                }

                return null;
            }
            catch(Exception ex)
            {
                LogManager.LogAsync("current playing", ex);
                return null;
            }
        }

        public async Task<string> CurrentPlayingTitle(string RadioId)
        {
            try
            {
                if (string.IsNullOrEmpty(RadioId)) return string.Empty;
                ReplayRadio tmp = await CurrentPlaying(RadioId);
                if (tmp != null)
                {
                    return tmp.Title;
                }
                else
                {
                    return string.Empty;
                }
            }
            catch(Exception ex)
            {
                LogManager.LogAsync("current playing title", ex);
                return string.Empty;
            }
        }

        public async Task<List<Categories>> RegionInfo()
        {
            try
            {
                List<Categories> list = await _api.GetRadioCategories(ServiceURLs.RegionRadios);

                return list;
            }
            catch(Exception ex)
            {
                LogManager.LogAsync("region info", ex);
                return null;
            }
        }

        public async Task<string> GetMediaId(string radioId)
        {
            try
            {
                return await _api.GetMdeiaInfo(string.Format(ServiceURLs.MediaInfo, radioId));
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get media id", ex);
                return null;
            }
        }

        public async Task<string> GetMediaThumb(string radioId)
        {
            try
            {
                string thumb = null;
                MediaListViewModel mediaList = MediaListViewModel.Instance;
                if (mediaList.MediaType ==  MusicItem.AudioType.None) return null;
                if (mediaList.MediaType ==  MusicItem.AudioType.Live || mediaList.MediaType ==  MusicItem.AudioType.Replay)
                {
                    AlbumInfo tmp = await _api.GetAlbumInfo(string.Format(ServiceURLs.AlbumInfo, radioId));
                    if(tmp != null) thumb = tmp.ImageUri;
                }
                else
                {
                    if (mediaList.LastestAlbumInfo != null)
                    {
                        thumb = mediaList.LastestAlbumInfo.ImageUri;
                    }
                }

                if (thumb == null) return null;

                return thumb.Replace("!200", "!400");
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get media thumb", ex);
                return null;
            }
        }

        /// <summary>
        /// 页签对应的目标日期：0=今天 1=昨天 2=前天。
        /// 统一用 DateTime.AddDays 计算，跨月/跨年（如 1 月 1-2 日）不会再取到 0 月而抛异常。
        /// </summary>
        public DateTime GetTargetDate(int ValueType)
        {
            int offset = ValueType < 0 ? 0 : ValueType;
            return DateTime.Now.AddDays(-offset);
        }

        /// <summary>
        /// 返回 (dayofweek, dayofmonth, month)。dayofweek 为接口约定：1=星期天 … 7=星期六。
        /// </summary>
        public Tuple<int, int, int> GetCurrentDay(int ValueType)
        {
            DateTime target = GetTargetDate(ValueType);
            int dayOfWeek = Convert.ToInt32(target.DayOfWeek) + 1;

            return Tuple.Create(dayOfWeek, target.Day, target.Month);
        }

        public async Task<AlbumInfo> GetAlbumInfo(string albumId)
        {
            try
            {
                if (albumId == null) return null;
                string _uri = string.Format(ServiceURLs.AlbumInfo, albumId);
                AlbumInfo info = await _api.GetAlbumInfo(_uri);

                return info;
            }
            catch (Exception ex)
            {
                LogManager.LogAsync("get album info", ex);
                return null;
            }
        }

        private PlayInfoHelper() { }
        public static readonly PlayInfoHelper Current = new PlayInfoHelper();
    }
}
