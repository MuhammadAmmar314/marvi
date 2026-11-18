using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MarViKaraoke.Models
{
    public class YouTubeSearchResult : INotifyPropertyChanged
    {
        private bool _isDownloading;
        private bool _isCompleted;
        private double _downloadProgress;
        private string _statusText = "⬇️ Unduh Lagu";

        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public TimeSpan? Duration { get; set; }
        public string DurationDisplay => Duration.HasValue ? Duration.Value.ToString(@"mm\:ss") : "--:--";
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string Url => $"https://www.youtube.com/watch?v={Id}";

        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                _isDownloading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanDownload));
                OnPropertyChanged(nameof(ShowProgressBar));
            }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                _isCompleted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanDownload));
                OnPropertyChanged(nameof(ShowProgressBar));
            }
        }

        public bool CanDownload => !_isDownloading;

        public bool ShowProgressBar => _isDownloading || _isCompleted;

        public double DownloadProgress
        {
            get => _downloadProgress;
            set
            {
                _downloadProgress = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
