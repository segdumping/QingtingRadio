using QingtingRadio.Tools;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace QingtingRadio.Https
{
    static class BaseService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.163 Safari/537.36 Edg/80.0.361.111";

        // Windows.Web.Http.HttpClient 没有 Timeout 属性，用每请求 CancellationTokenSource 实现超时
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

        static BaseService()
        {
            // UA 只设置一次。此前每个请求都 Clear()/Add() 共享的 DefaultRequestHeaders，
            // 并发请求会互相清头甚至抛异常。
            _httpClient.DefaultRequestHeaders.TryAppendWithoutValidation("User-Agent", UserAgent);
        }

        /// <summary>
        /// 最近一次请求失败的原因（状态码或异常消息），供调用方诊断"为什么拿到 null"。
        /// 所有失败同时会写入日志。
        /// </summary>
        public static string LastError { get; private set; }

        private static void RecordError(string url, string message)
        {
            LastError = message;
            Debug.WriteLine("request failed: {0} ({1})", url, message);
            LogManager.LogAsync("request failed " + message + " " + url);
        }

        private static async Task<HttpResponseMessage> GetWithTimeoutAsync(Uri uri)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(RequestTimeout))
            {
                return await _httpClient.GetAsync(uri).AsTask(cts.Token);
            }
        }

        /// <summary>
        /// 向服务器发送get请求  返回服务器回复数据(string)
        /// </summary>
        public async static Task<string> SendGetRequest(string url)
        {
            try
            {
                HttpResponseMessage response = await GetWithTimeoutAsync(new Uri(url));
                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        RecordError(url, "HTTP " + (int)response.StatusCode);
                        return null;
                    }

                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                RecordError(url, ex.Message);
                return null;
            }
        }
        /// <summary>
        /// 向服务器发送get请求  返回服务器回复数据(bytes)
        /// </summary>
        public async static Task<IBuffer> SendGetRequestAsBytes(string url)
        {
            try
            {
                HttpResponseMessage response = await GetWithTimeoutAsync(new Uri(url));
                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        RecordError(url, "HTTP " + (int)response.StatusCode);
                        return null;
                    }

                    return await response.Content.ReadAsBufferAsync();
                }
            }
            catch (Exception ex)
            {
                RecordError(url, ex.Message);
                return null;
            }
        }
        /// <summary>
        /// 向服务器发送post请求 返回服务器回复数据(string)
        /// </summary>
        public async static Task<string> SendPostRequest(string url, string body)
        {
            try
            {
                HttpRequestMessage mSent = new HttpRequestMessage(HttpMethod.Post, new Uri(url))
                {
                    Content = new HttpStringContent(body, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json; charset=utf-8")
                };

                using (CancellationTokenSource cts = new CancellationTokenSource(RequestTimeout))
                {
                    HttpResponseMessage response = await _httpClient.SendRequestAsync(mSent).AsTask(cts.Token);
                    using (response)
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            RecordError(url, "HTTP " + (int)response.StatusCode);
                            return null;
                        }

                        return await response.Content.ReadAsStringAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                RecordError(url, ex.Message);
                return null;
            }
        }
    }
}
