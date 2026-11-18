using System;

namespace MarViKaraoke.Models
{
    public class UpdateInfo
    {
        public string CurrentVersion { get; set; } = "2.0.0";
        public string LatestVersion { get; set; } = "2.0.0";
        public string Title { get; set; } = string.Empty;
        public string Changelog { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; } = 0;
        public bool IsUpdateAvailable { get; set; } = false;
        public DateTime? PublishedAt { get; set; }

        public string FormattedSize
        {
            get
            {
                if (FileSizeBytes <= 0) return string.Empty;
                double mb = FileSizeBytes / (1024.0 * 1024.0);
                return $"{mb:F1} MB";
            }
        }
    }
}
