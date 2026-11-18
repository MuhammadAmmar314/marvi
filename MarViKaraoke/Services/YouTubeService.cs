using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MarViKaraoke.Models;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace MarViKaraoke.Services
{
    public class YouTubeService
    {
        private readonly YoutubeClient _client = new YoutubeClient();
        private readonly LyricsService _lyricsService = new LyricsService();

        public async Task<List<YouTubeSearchResult>> SearchAsync(string query, int limit = 20)
        {
            var results = new List<YouTubeSearchResult>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            var searchResults = _client.Search.GetVideosAsync(query);
            int count = 0;

            await foreach (var video in searchResults)
            {
                results.Add(new YouTubeSearchResult
                {
                    Id = video.Id.Value,
                    Title = video.Title,
                    Author = video.Author.ChannelTitle,
                    Duration = video.Duration,
                    ThumbnailUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url ?? ""
                });

                count++;
                if (count >= limit) break;
            }

            return results;
        }

        public async Task<string> DownloadAudioAsync(string videoId, string targetDirectory, IProgress<double>? progress = null)
        {
            var video = await _client.Videos.GetAsync(videoId);
            var streamManifest = await _client.Videos.Streams.GetManifestAsync(videoId);

            // Ambil audio stream dengan bitrate terbaik
            var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            if (audioStreamInfo == null)
                throw new InvalidOperationException("Tidak ditemukan audio stream pada video YouTube ini.");

            string safeTitle = SanitizeFileName(video.Title);
            string ext = audioStreamInfo.Container.Name.ToLower(); // "m4a", "webm", dll
            string outputFilePath = Path.Combine(targetDirectory, $"{safeTitle}.{ext}");

            int index = 1;
            while (File.Exists(outputFilePath))
            {
                outputFilePath = Path.Combine(targetDirectory, $"{safeTitle} ({index}).{ext}");
                index++;
            }

            // Unduh file audio
            await _client.Videos.Streams.DownloadAsync(audioStreamInfo, outputFilePath, progress);

            // Jika formatnya bukan WAV atau MP3 (misal .webm atau .m4a), konversikan otomatis ke .wav untuk jaminan kompatibilitas 100%
            string finalAudioPath = outputFilePath;
            if (ext != "wav" && ext != "mp3")
            {
                try
                {
                    string convertedWav = Path.ChangeExtension(outputFilePath, ".wav");
                    using (var reader = new NAudio.Wave.MediaFoundationReader(outputFilePath))
                    {
                        NAudio.Wave.WaveFileWriter.CreateWaveFile(convertedWav, reader);
                    }
                    if (File.Exists(convertedWav))
                    {
                        File.Delete(outputFilePath);
                        finalAudioPath = convertedWav;
                    }
                }
                catch { }
            }

            // Ambil atau generate lirik sinkron otomatis (.lrc) dari YouTube Subtitles / LRCLIB / Deskripsi
            string lrcPath = Path.Combine(Path.GetDirectoryName(finalAudioPath)!, $"{Path.GetFileNameWithoutExtension(finalAudioPath)}.lrc");
            if (!File.Exists(lrcPath))
            {
                double durationSec = video.Duration.HasValue ? video.Duration.Value.TotalSeconds : 180;
                string? autoLyrics = await _lyricsService.FetchLyricsForVideoAsync(_client, videoId, video.Title, video.Author.ChannelTitle, durationSec);
                if (!string.IsNullOrEmpty(autoLyrics))
                {
                    try { File.WriteAllText(lrcPath, autoLyrics); } catch { }
                }
            }

            return finalAudioPath;
        }

        private static string SanitizeFileName(string name)
        {
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            string regexSearch = $"[{Regex.Escape(invalid)}]";
            string cleaned = Regex.Replace(name, regexSearch, "");
            return string.IsNullOrWhiteSpace(cleaned) ? "YouTube_Audio" : cleaned.Trim();
        }
    }
}
