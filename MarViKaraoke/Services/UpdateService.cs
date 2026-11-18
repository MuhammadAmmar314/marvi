using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using MarViKaraoke.Models;

namespace MarViKaraoke.Services
{
    public class UpdateService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _configFile;

        public string GitHubRepo { get; set; } = "MuhammadAmmar314/marvi";

        public UpdateService()
        {
            _configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updater_config.json");
            LoadConfig();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configFile))
                {
                    string json = File.ReadAllText(_configFile);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("GitHubRepo", out var repoProp))
                    {
                        string? repo = repoProp.GetString();
                        if (!string.IsNullOrWhiteSpace(repo) && !repo.Equals("Ammar-Official/MarViKaraoke", StringComparison.OrdinalIgnoreCase))
                        {
                            GitHubRepo = repo.Trim();
                        }
                    }
                }
            }
            catch
            {
                // Pengaman parsing config
            }
        }

        public void SaveConfig(string repo)
        {
            try
            {
                GitHubRepo = repo.Trim();
                var data = new { GitHubRepo = GitHubRepo };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFile, json);
            }
            catch
            {
                // Pengaman simpan config
            }
        }

        public Version GetCurrentVersion()
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            return ver ?? new Version(2, 0, 0);
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync(string? repoOverride = null)
        {
            string targetRepo = string.IsNullOrWhiteSpace(repoOverride) ? GitHubRepo : repoOverride.Trim();
            var currentVersion = GetCurrentVersion();
            string currentVerStr = $"{currentVersion.Major}.{currentVersion.Minor}.{Math.Max(0, currentVersion.Build)}";

            var result = new UpdateInfo
            {
                CurrentVersion = $"v{currentVerStr}",
                LatestVersion = $"v{currentVerStr}",
                IsUpdateAvailable = false
            };

            if (string.IsNullOrWhiteSpace(targetRepo) || !targetRepo.Contains('/'))
            {
                result.Changelog = "Format repositori GitHub belum valid (Contoh format: username/repository).";
                return result;
            }

            try
            {
                string apiUrl = $"https://api.github.com/repos/{targetRepo}/releases/latest";
                using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.UserAgent.Clear();
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MarViKaraoke-App", currentVerStr));

                using var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    result.Changelog = $"Repositori '{targetRepo}' atau rilis terbaru belum ditemukan di GitHub.";
                    return result;
                }

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string rawTag = root.GetProperty("tag_name").GetString() ?? "0.0.0";
                string cleanTag = rawTag.TrimStart('v', 'V').Trim();
                string releaseTitle = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? rawTag : rawTag;
                string releaseBody = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;

                DateTime? publishedAt = null;
                if (root.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTime(out var dt))
                {
                    publishedAt = dt;
                }

                result.LatestVersion = rawTag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? rawTag : $"v{cleanTag}";
                result.Title = releaseTitle;
                result.Changelog = string.IsNullOrWhiteSpace(releaseBody) ? "Tidak ada catatan rilis khusus." : releaseBody;
                result.PublishedAt = publishedAt;

                // Cari asset file zip installer
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string assetName = asset.GetProperty("name").GetString() ?? string.Empty;
                        if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            result.DownloadUrl = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                            if (asset.TryGetProperty("size", out var sizeProp))
                            {
                                result.FileSizeBytes = sizeProp.GetInt64();
                            }
                            break;
                        }
                    }
                }

                // Jika tidak ada aset zip, gunakan zipball source sebagai fallback
                if (string.IsNullOrEmpty(result.DownloadUrl) && root.TryGetProperty("zipball_url", out var zipballProp))
                {
                    result.DownloadUrl = zipballProp.GetString() ?? string.Empty;
                }

                // Bandingkan versi
                if (Version.TryParse(cleanTag, out var remoteVersion))
                {
                    // Normalisasi komponen versi
                    var v1 = new Version(remoteVersion.Major, remoteVersion.Minor, Math.Max(0, remoteVersion.Build));
                    var v0 = new Version(currentVersion.Major, currentVersion.Minor, Math.Max(0, currentVersion.Build));
                    if (v1 > v0)
                    {
                        result.IsUpdateAvailable = true;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Changelog = $"Gagal memeriksa pembaruan: {ex.Message}";
            }

            return result;
        }

        public async Task<string> DownloadUpdateZipAsync(string downloadUrl, IProgress<double>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
                throw new ArgumentException("URL unduhan rilis tidak valid.");

            string tempZipPath = Path.Combine(Path.GetTempPath(), $"MarViUpdate_{Guid.NewGuid():N}.zip");

            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.UserAgent.Clear();
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MarViKaraoke-App", "2.0.0"));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    double pct = (double)totalRead * 100.0 / totalBytes.Value;
                    progress?.Report(Math.Clamp(pct, 0.0, 100.0));
                }
            }

            progress?.Report(100.0);
            return tempZipPath;
        }

        public static void InstallAndRestart(string zipFilePath)
        {
            if (!File.Exists(zipFilePath))
                throw new FileNotFoundException("Berkas pembaruan tidak ditemukan.", zipFilePath);

            string extractDir = Path.Combine(Path.GetTempPath(), "MarVi_Update_Extract");
            if (Directory.Exists(extractDir))
            {
                try { Directory.Delete(extractDir, true); } catch { }
            }
            Directory.CreateDirectory(extractDir);

            // Ekstrak file zip
            ZipFile.ExtractToDirectory(zipFilePath, extractDir, true);

            // Cari Setup_MarVi_Karaoke.bat
            string? setupBat = null;
            string directBat = Path.Combine(extractDir, "Setup_MarVi_Karaoke.bat");
            if (File.Exists(directBat))
            {
                setupBat = directBat;
            }
            else
            {
                var files = Directory.GetFiles(extractDir, "Setup_MarVi_Karaoke.bat", SearchOption.AllDirectories);
                if (files.Length > 0) setupBat = files[0];
            }

            if (string.IsNullOrEmpty(setupBat) || !File.Exists(setupBat))
            {
                throw new InvalidOperationException("Berkas 'Setup_MarVi_Karaoke.bat' tidak ditemukan di dalam paket rilis.");
            }

            // Jalankan installer pembaruan di latar belakang
            var psi = new ProcessStartInfo
            {
                FileName = setupBat,
                WorkingDirectory = Path.GetDirectoryName(setupBat),
                UseShellExecute = true
            };

            Process.Start(psi);

            // Tutup aplikasi sekarang juga agar berkas exe tidak terkunci
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown();
            });
        }
    }
}
