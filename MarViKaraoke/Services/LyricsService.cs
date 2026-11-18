using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.ClosedCaptions;

namespace MarViKaraoke.Services
{
    /// <summary>
    /// Layanan pencarian dan sinkronisasi lirik menggunakan LRCLIB API resmi (https://lrclib.net/docs)
    /// dan Closed Captions bawaan YouTube.
    /// </summary>
    public class LyricsService
    {
        private const string LrclibBaseUrl = "https://lrclib.net";

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        static LyricsService()
        {
            // Sesuai dokumentasi LRCLIB: wajib menyertakan User-Agent aplikasi yang jelas
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MarVi-Karaoke/2.0 (https://github.com/marvi-karaoke; contact: dev@marvikaraoke.local)");
        }

        /// <summary>
        /// Ambil lirik sinkron otomatis (.lrc) untuk video YouTube:
        /// 1. LRCLIB API (Exact match / Search)
        /// 2. YouTube Closed Captions
        /// 3. Ekstraksi Deskripsi Video
        /// </summary>
        public async Task<string?> FetchLyricsForVideoAsync(YoutubeClient client, string videoId, string title, string author, double durationSeconds)
        {
            // 1. Prioritas Utama: Database Lirik Terbuka Global LRCLIB (https://lrclib.net/docs)
            string? lrclibLyrics = await FetchFromLrclibAsync(title, author, durationSeconds);
            if (!string.IsNullOrEmpty(lrclibLyrics))
            {
                return lrclibLyrics;
            }

            // 2. Prioritas Kedua: YouTube Closed Captions / Subtitles bawaan video
            try
            {
                var trackManifest = await client.Videos.ClosedCaptions.GetManifestAsync(videoId);
                var track = trackManifest.TryGetByLanguage("id")
                         ?? trackManifest.TryGetByLanguage("en")
                         ?? trackManifest.Tracks.FirstOrDefault();

                if (track != null)
                {
                    var trackInfo = await client.Videos.ClosedCaptions.GetAsync(track);
                    var sb = new StringBuilder();
                    sb.AppendLine($"[ti:{title}]");
                    sb.AppendLine($"[ar:{author}]");
                    sb.AppendLine($"[re:YouTube ClosedCaptions]");

                    int validCount = 0;
                    foreach (var c in trackInfo.Captions)
                    {
                        if (string.IsNullOrWhiteSpace(c.Text)) continue;
                        int m = (int)c.Offset.TotalMinutes;
                        int s = c.Offset.Seconds;
                        int cs = c.Offset.Milliseconds / 10;
                        string cleanLine = Regex.Replace(c.Text, @"\r?\n", " ").Trim();
                        sb.AppendLine($"[{m:D2}:{s:D2}.{cs:D2}]{cleanLine}");
                        validCount++;
                    }

                    if (validCount >= 4)
                    {
                        return sb.ToString();
                    }
                }
            }
            catch { }

            // 3. Prioritas Ketiga: Ekstraksi Lirik dari Deskripsi Video YouTube
            try
            {
                var video = await client.Videos.GetAsync(videoId);
                if (!string.IsNullOrWhiteSpace(video.Description))
                {
                    string? descLyrics = ExtractLyricsFromDescription(video.Description, durationSeconds, title, author);
                    if (!string.IsNullOrEmpty(descLyrics))
                    {
                        return descLyrics;
                    }
                }
            }
            catch { }

            // Fallback Template
            return $"[ti:{title}]\n[ar:{author}]\n[00:00.00]{title} - {author}\n[00:03.00]Selamat Bernyanyi! 🎤\n";
        }

        /// <summary>
        /// Mengambil lirik dari LRCLIB menggunakan endpoint resmi:
        /// 1. GET /api/get (Exact match: track_name, artist_name, duration)
        /// 2. GET /api/search (Search query: track_name, artist_name, q)
        /// Dokumentasi: https://lrclib.net/docs
        /// </summary>
        public async Task<string?> FetchFromLrclibAsync(string rawTitle, string rawArtist, double durationSeconds)
        {
            var (trackName, artistName) = ParseSongAndArtist(rawTitle, rawArtist);

            if (string.IsNullOrWhiteSpace(trackName)) return null;

            // --- LANGKAH 1: GET /api/get (Exact Match Lookup) ---
            try
            {
                var queryParams = new List<string>
                {
                    $"track_name={Uri.EscapeDataString(trackName)}"
                };

                if (!string.IsNullOrEmpty(artistName))
                {
                    queryParams.Add($"artist_name={Uri.EscapeDataString(artistName)}");
                }

                if (durationSeconds > 0)
                {
                    queryParams.Add($"duration={(int)Math.Round(durationSeconds)}");
                }

                string getUrl = $"{LrclibBaseUrl}/api/get?{string.Join("&", queryParams)}";
                var response = await _httpClient.GetAsync(getUrl);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("syncedLyrics", out var synced) && synced.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(synced.GetString()))
                    {
                        return synced.GetString();
                    }

                    if (root.TryGetProperty("plainLyrics", out var plain) && plain.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(plain.GetString()))
                    {
                        return ConvertPlainLyricsToLrc(plain.GetString()!, durationSeconds, trackName, artistName);
                    }
                }
            }
            catch { }

            // --- LANGKAH 2: GET /api/search (Specific Filters) ---
            try
            {
                var searchQueries = new List<string>();

                // Pola 1: Filter terstruktur track_name & artist_name
                if (!string.IsNullOrEmpty(artistName))
                {
                    searchQueries.Add($"track_name={Uri.EscapeDataString(trackName)}&artist_name={Uri.EscapeDataString(artistName)}");
                    searchQueries.Add($"q={Uri.EscapeDataString($"{trackName} {artistName}")}");
                }

                // Pola 2: Query bebas track_name saja
                searchQueries.Add($"q={Uri.EscapeDataString(trackName)}");

                foreach (var queryString in searchQueries)
                {
                    string searchUrl = $"{LrclibBaseUrl}/api/search?{queryString}";
                    var response = await _httpClient.GetAsync(searchUrl);

                    if (!response.IsSuccessStatusCode) continue;

                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) continue;

                    JsonElement? bestSynced = null;
                    JsonElement? bestPlain = null;
                    double minDurationDiff = double.MaxValue;

                    foreach (var item in root.EnumerateArray())
                    {
                        double itemDuration = 0;
                        if (item.TryGetProperty("duration", out var durProp) && durProp.TryGetDouble(out var dur))
                        {
                            itemDuration = dur;
                        }

                        double diff = durationSeconds > 0 ? Math.Abs(itemDuration - durationSeconds) : 0;

                        // Cari yang memiliki syncedLyrics
                        if (item.TryGetProperty("syncedLyrics", out var syn) && syn.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(syn.GetString()))
                        {
                            if (bestSynced == null || diff < minDurationDiff)
                            {
                                bestSynced = item;
                                minDurationDiff = diff;
                            }
                        }

                        // Fallback plainLyrics
                        if (bestPlain == null && item.TryGetProperty("plainLyrics", out var pl) && pl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(pl.GetString()))
                        {
                            bestPlain = item;
                        }
                    }

                    if (bestSynced.HasValue)
                    {
                        return bestSynced.Value.GetProperty("syncedLyrics").GetString();
                    }

                    if (bestPlain.HasValue)
                    {
                        string plainText = bestPlain.Value.GetProperty("plainLyrics").GetString()!;
                        return ConvertPlainLyricsToLrc(plainText, durationSeconds, trackName, artistName);
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Mengurai judul lagu dan nama artis dari string judul YouTube yang bervariasi
        /// Contoh:
        /// - "Selimut hati - Dewa 19, Virzha - Lirik Lagu" -> Track: "Selimut Hati", Artist: "Dewa 19"
        /// - "Noah - Separuh Aku (Official Video)" -> Track: "Separuh Aku", Artist: "Noah"
        /// </summary>
        public static (string TrackName, string ArtistName) ParseSongAndArtist(string rawTitle, string rawAuthor)
        {
            string clean = CleanSongTitle(rawTitle);
            string artist = CleanArtistName(rawAuthor);

            if (clean.Contains(" - "))
            {
                var parts = clean.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    // Pola: "Artis - Judul" atau "Judul - Artis"
                    if (string.Equals(parts[0].Trim(), artist, StringComparison.OrdinalIgnoreCase))
                    {
                        return (parts[1].Trim(), parts[0].Trim());
                    }
                    else if (string.Equals(parts[1].Trim(), artist, StringComparison.OrdinalIgnoreCase))
                    {
                        return (parts[0].Trim(), parts[1].Trim());
                    }
                    else
                    {
                        // Default jika author youtube tidak cocok: anggap bagian pertama judul, bagian kedua artis
                        return (parts[0].Trim(), parts[1].Trim());
                    }
                }
                else if (parts.Length > 2)
                {
                    // Contoh: "Selimut hati - Dewa 19, Virzha - Lirik Lagu"
                    // Part 0 = Selimut hati, Part 1 = Dewa 19, Virzha
                    string track = parts[0].Trim();
                    string art = parts[1].Trim();
                    return (track, art);
                }
            }

            return (clean, artist);
        }

        public string ConvertPlainLyricsToLrc(string plainText, double totalDuration, string title, string artist)
        {
            var rawLines = plainText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var validLines = new List<string>();

            foreach (var line in rawLines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Abaikan section header [Verse], [Chorus], dll
                if (Regex.IsMatch(trimmed, @"^\[(verse|chorus|bridge|pre-chorus|outro|intro|hook|solo|instrumental|interlude).*\]$", RegexOptions.IgnoreCase))
                    continue;

                validLines.Add(trimmed);
            }

            if (validLines.Count == 0)
            {
                return $"[ti:{title}]\n[ar:{artist}]\n[00:00.00]{title} - {artist}\n[00:03.00]Selamat Bernyanyi! 🎤\n";
            }

            double dur = totalDuration > 30 ? totalDuration : 180;
            double introTime = Math.Min(12, Math.Max(4, dur * 0.05));
            double outroBuffer = Math.Min(10, Math.Max(4, dur * 0.04));
            double singingDuration = Math.Max(20, dur - introTime - outroBuffer);
            double timePerLine = singingDuration / validLines.Count;

            var sb = new StringBuilder();
            sb.AppendLine($"[ti:{title}]");
            sb.AppendLine($"[ar:{artist}]");
            sb.AppendLine($"[re:MarVi Karaoke Auto-Sync LRCLIB]");
            sb.AppendLine($"[00:00.00]{title} {(!string.IsNullOrEmpty(artist) ? "- " + artist : "")}");
            sb.AppendLine($"[00:02.50]♪ Musik ♪");

            for (int i = 0; i < validLines.Count; i++)
            {
                double lineTime = introTime + i * timePerLine;
                int m = (int)(lineTime / 60);
                int s = (int)(lineTime % 60);
                int cs = (int)((lineTime % 1) * 100);
                sb.AppendLine($"[{m:D2}:{s:D2}.{cs:D2}]{validLines[i]}");
            }

            return sb.ToString();
        }

        private string? ExtractLyricsFromDescription(string description, double duration, string title, string artist)
        {
            var lines = description.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var lyricCandidates = new List<string>();
            bool capturing = false;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (Regex.IsMatch(trimmed, @"^(lirik|lyrics)\s*[:：]?", RegexOptions.IgnoreCase))
                {
                    capturing = true;
                    continue;
                }

                if (capturing)
                {
                    if (Regex.IsMatch(trimmed, @"^(subscribe|follow|instagram|twitter|facebook|copyright|prod by|arranged by|mixed by|music video|http)", RegexOptions.IgnoreCase))
                    {
                        if (lyricCandidates.Count >= 6) break;
                    }

                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        lyricCandidates.Add(trimmed);
                    }
                }
            }

            if (lyricCandidates.Count >= 6)
            {
                return ConvertPlainLyricsToLrc(string.Join("\n", lyricCandidates), duration, title, artist);
            }

            return null;
        }

        public static string CleanSongTitle(string title)
        {
            string clean = title;
            clean = Regex.Replace(clean, @"\.[a-zA-Z0-9]{2,4}$", "");
            clean = Regex.Replace(clean, @"\([^\)]*\)", "");
            clean = Regex.Replace(clean, @"\[[^\]]*\]", "");
            clean = Regex.Replace(clean, @"\b(official music video|official video|music video|lirik lagu|lirik video|lyrics video|lyrics|audio|cover|video klip)\b", "", RegexOptions.IgnoreCase);
            return clean.Trim();
        }

        public static string CleanArtistName(string artist)
        {
            string clean = artist;
            clean = Regex.Replace(clean, @"\b(official|channel|vevo|records|topic)\b", "", RegexOptions.IgnoreCase);
            clean = Regex.Replace(clean, @"[^\w\s,]", "").Trim();
            return clean;
        }
    }
}
