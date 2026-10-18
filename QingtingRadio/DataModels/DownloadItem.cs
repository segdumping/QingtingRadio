using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QingtingRadio.DataModels
{
    public class DownloadItem : MusicItem, INotifyPropertyChanged
    {
        private Int32 progress;
        private double fileSize;
        public string AlbumName { get; set; }
        public string AlubmId { get; set; }       
        public string AudioId { get; set; }
        public string AudioTime { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string FileUri { get; set; }
        public string Duration { get; set; }
        public bool IsSuccess { get; set; }
        public string FileHash { get; set; }

        public Int32 Progress
        {
            get
            {
                return progress;
            }
            set
            {
                if(progress != value)
                {
                    progress = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Progress)));
                }
            }
        }

        public double FileSize
        {
            get
            {
                return fileSize;
            }
            set
            {
                if (fileSize != value)
                {
                    fileSize = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize)));
                }
            }
        }

        public Guid  ID
        {
            get
            {
                if (!string.IsNullOrEmpty(Id))
                {
                    return new Guid(Id);
                }
                else
                {
                    return Guid.Empty;
                }
            }
            set
            {
                Id = value.ToString();
            }
        }

        public DownloadItem()
        {

        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
