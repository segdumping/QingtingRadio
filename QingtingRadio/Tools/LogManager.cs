using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace QingtingRadio.Tools
{
    /// <summary>
    /// 日志：写入本地单文件，超 2MB 滚动重建；自身异常静默，不向外抛出。
    /// </summary>
    public class LogManager
    {
        // 用 SemaphoreSlim 而不是 Monitor：await 之后的续体可能换线程执行，
        // Monitor.Exit 会因跨线程释放抛 SynchronizationLockException 并导致锁永久丢失、日志静默停写。
        private static readonly SemaphoreSlim mutex = new SemaphoreSlim(1, 1);

        public static void LogAsync(string message, Exception ex)
        {
            _ = Task.Run(async () =>
            {
                await mutex.WaitAsync();
                try
                {
                    StorageFolder folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("DatasCache", CreationCollisionOption.OpenIfExists);

                    // 超过 2MB 时先删旧文件，从头开始记录
                    StorageFile oldFile = await folder.TryGetItemAsync("QingtingLog.txt") as StorageFile;
                    if (oldFile != null)
                    {
                        BasicProperties property = await oldFile.GetBasicPropertiesAsync();
                        if (property.Size > 1024 * 1024 * 2)
                        {
                            await oldFile.DeleteAsync();
                        }
                    }

                    using (Stream fileStream = await folder.OpenStreamForWriteAsync("QingtingLog.txt", CreationCollisionOption.OpenIfExists))
                    {
                        // 定位到文件末尾追加；不 Seek 会从 0 开始覆盖写，历史日志全部丢失
                        fileStream.Seek(0, SeekOrigin.End);

                        using (StreamWriter write = new StreamWriter(fileStream))
                        {
                            string msg = DateTime.Now.ToString() + "||" + message;
                            if (ex != null)
                            {
                                msg += "||" + ex.Message + ex.Source + ex.StackTrace;
                            }
                            await write.WriteLineAsync(msg);
                            await write.WriteLineAsync("----------------------------------------------");
                            await write.FlushAsync();
                        }
                    }
                }
                catch (Exception)
                {
                    // 日志系统自身失败时静默，绝不能向外抛异常
                }
                finally
                {
                    mutex.Release();
                }
            });
        }

        public static void LogAsync(string message)
        {
            LogAsync(message, null);
        }
    }
}