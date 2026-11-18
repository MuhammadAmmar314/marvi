using System;
using System.IO;

namespace MarViKaraoke.Models
{
    public class SongItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = "Artis Lokal";
        public double DurationSeconds { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? InstrumentalFilePath { get; set; }
        public string? LyricsPath { get; set; }
        public string? LyricsContent { get; set; }
        public bool HasLyrics => !string.IsNullOrEmpty(LyricsContent) || (!string.IsNullOrEmpty(LyricsPath) && File.Exists(LyricsPath));
        public bool HasAiInstrumental => !string.IsNullOrEmpty(InstrumentalFilePath) && File.Exists(InstrumentalFilePath);
        public bool IsVideo { get; set; }

        public string DurationDisplay
        {
            get
            {
                var ts = TimeSpan.FromSeconds(DurationSeconds);
                return ts.Hours > 0 ? ts.ToString(@"hh\:mm\:ss") : ts.ToString(@"mm\:ss");
            }
        }
    }
}
