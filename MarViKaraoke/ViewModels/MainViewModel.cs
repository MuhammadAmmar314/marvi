using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using MarViKaraoke.Core;
using MarViKaraoke.Models;
using MarViKaraoke.Services;

namespace MarViKaraoke.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly AudioEngine _audioEngine;
        private readonly DispatcherTimer _syncTimer;

        private SongItem? _currentSong;
        private string _currentSinger = "Tamu";
        private bool _isPlaying;
        private double _currentPosition;
        private double _totalDuration;
        private int _pitchSemitones = 0;
        private float _tempoRate = 1.0f;
        private float _volume = 0.9f;
        private float _vocalAttenuation = 0f;
        private double _lyricOffsetSeconds = 0;
        private string _activeLineText = "♪ Selamat Datang di MarVi Karaoke .NET ♪";
        private string _nextLineText = "Pilih lagu untuk memulai bernyanyi";
        private ObservableCollection<LyricWord> _currentWords = new ObservableCollection<LyricWord>();

        private List<LyricLine> _parsedLines = new List<LyricLine>();
        private LyricLine? _lastRenderedLine = null;

        public ObservableCollection<SongItem> Catalog { get; } = new ObservableCollection<SongItem>();
        public ObservableCollection<QueueItem> Queue { get; } = new ObservableCollection<QueueItem>();

        private readonly YouTubeService _youtubeService = new YouTubeService();
        private readonly LyricsService _lyricsService = new LyricsService();
        public ObservableCollection<YouTubeSearchResult> YouTubeResults { get; } = new ObservableCollection<YouTubeSearchResult>();

        private string _youtubeQuery = "";
        public string YouTubeQuery
        {
            get => _youtubeQuery;
            set { _youtubeQuery = value; OnPropertyChanged(); }
        }

        private bool _isSearchingYouTube;
        public bool IsSearchingYouTube
        {
            get => _isSearchingYouTube;
            set { _isSearchingYouTube = value; OnPropertyChanged(); }
        }

        public event Action<SongItem?, string, string, ObservableCollection<LyricWord>>? OnSingerViewSync;

        public SongItem? CurrentSong
        {
            get => _currentSong;
            set { _currentSong = value; OnPropertyChanged(); }
        }

        public string CurrentSinger
        {
            get => _currentSinger;
            set { _currentSinger = value; OnPropertyChanged(); }
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnPropertyChanged(); }
        }

        public double CurrentPosition
        {
            get => _currentPosition;
            set
            {
                if (Math.Abs(_currentPosition - value) > 0.01)
                {
                    _currentPosition = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentTimeDisplay));
                }
            }
        }

        public double TotalDuration
        {
            get => _totalDuration;
            set
            {
                _totalDuration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalDurationDisplay));
            }
        }

        public string CurrentTimeDisplay => TimeSpan.FromSeconds(CurrentPosition).ToString(@"mm\:ss");
        public string TotalDurationDisplay => TimeSpan.FromSeconds(TotalDuration).ToString(@"mm\:ss");

        public int PitchSemitones
        {
            get => _pitchSemitones;
            set
            {
                _pitchSemitones = Math.Clamp(value, -6, 6);
                _audioEngine.SetPitch(_pitchSemitones);
                OnPropertyChanged();
                OnPropertyChanged(nameof(PitchDisplay));
            }
        }

        public string PitchDisplay => _pitchSemitones > 0 ? $"+{_pitchSemitones}" : _pitchSemitones.ToString();

        public float TempoRate
        {
            get => _tempoRate;
            set
            {
                _tempoRate = Math.Clamp(value, 0.75f, 1.25f);
                _audioEngine.SetTempo(_tempoRate);
                OnPropertyChanged();
                OnPropertyChanged(nameof(TempoDisplay));
            }
        }

        public string TempoDisplay => $"{_tempoRate:F2}x";

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0f, 1f);
                _audioEngine.SetVolume(_volume);
                OnPropertyChanged();
            }
        }

        public float VocalAttenuation
        {
            get => _vocalAttenuation;
            set
            {
                float oldVal = _vocalAttenuation;
                _vocalAttenuation = Math.Clamp(value, 0f, 1f);
                _audioEngine.SetVocalAttenuation(_vocalAttenuation);

                // Jika lagu memiliki file instrumental AI yang bersih:
                if (CurrentSong != null && CurrentSong.HasAiInstrumental)
                {
                    bool wasMuted = oldVal >= 0.5f;
                    bool shouldMute = _vocalAttenuation >= 0.5f;

                    if (wasMuted != shouldMute && IsPlaying)
                    {
                        double pos = _audioEngine.GetPositionSeconds();
                        string targetPath = shouldMute ? CurrentSong.InstrumentalFilePath! : CurrentSong.FilePath;
                        if (_audioEngine.LoadSong(targetPath))
                        {
                            _audioEngine.Seek(pos);
                            _audioEngine.Play();
                        }
                    }
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(VocalAttenuationPercent));
                OnPropertyChanged(nameof(VocalButtonText));
            }
        }

        public string VocalAttenuationPercent => $"{Math.Round(_vocalAttenuation * 100)}%";
        public string VocalButtonText => _vocalAttenuation >= 0.5f ? "🔇 Vokal: Senyap" : "🎙️ Vokal: Aktif";

        public double LyricOffsetSeconds
        {
            get => _lyricOffsetSeconds;
            set
            {
                _lyricOffsetSeconds = Math.Round(value, 1);
                OnPropertyChanged();
                OnPropertyChanged(nameof(LyricOffsetDisplay));
            }
        }

        public string LyricOffsetDisplay => _lyricOffsetSeconds > 0 ? $"+{_lyricOffsetSeconds:F1}s" : $"{_lyricOffsetSeconds:F1}s";

        public string ActiveLineText
        {
            get => _activeLineText;
            set { _activeLineText = value; OnPropertyChanged(); }
        }

        public string NextLineText
        {
            get => _nextLineText;
            set { _nextLineText = value; OnPropertyChanged(); }
        }

        public ObservableCollection<LyricWord> CurrentWords
        {
            get => _currentWords;
            set { _currentWords = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            _audioEngine = new AudioEngine();
            _audioEngine.SongEnded += OnSongEnded;

            _syncTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(25) // High-precision 40fps sync
            };
            _syncTimer.Tick += OnSyncTick;

            ScanDefaultSongsDirectory();
        }

        public void ScanDefaultSongsDirectory()
        {
            Catalog.Clear();
            string songsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "songs");
            if (!Directory.Exists(songsDir))
            {
                // Fallback ke folder songs di project root
                string altDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\songs");
                if (Directory.Exists(altDir)) songsDir = Path.GetFullPath(altDir);
            }

            if (!Directory.Exists(songsDir)) return;

            var mediaExtensions = new[] { ".mp3", ".wav", ".m4a", ".ogg", ".mp4", ".mkv", ".webm", ".opus", ".aac", ".flac", ".wma" };
            var files = Directory.GetFiles(songsDir);

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                // Jangan masukkan file stem vokal atau instrumen sebagai item lagu terpisah
                if (fileName.EndsWith("_music.wav", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith("_vocals.wav", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith("_instrumental.wav", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string ext = Path.GetExtension(file).ToLower();
                if (mediaExtensions.Contains(ext))
                {
                    string baseName = Path.GetFileNameWithoutExtension(file);
                    string lrcPath = Path.Combine(songsDir, $"{baseName}.lrc");
                    string title = baseName;
                    string artist = "Artis Lokal";
                    double duration = 0;

                    // Periksa apakah file instrumental hasil AI OwnVocalRemover sudah tersedia
                    string? instPath = null;
                    string cand1 = Path.Combine(songsDir, $"{baseName}_music.wav");
                    string cand2 = Path.Combine(songsDir, $"{baseName}_instrumental.wav");
                    string cand3 = Path.Combine(songsDir, "output", $"{baseName}_music.wav");

                    if (File.Exists(cand1)) instPath = cand1;
                    else if (File.Exists(cand2)) instPath = cand2;
                    else if (File.Exists(cand3)) instPath = cand3;

                    try
                    {
                        using var tagFile = TagLib.File.Create(file);
                        if (!string.IsNullOrEmpty(tagFile.Tag.Title)) title = tagFile.Tag.Title;
                        if (!string.IsNullOrEmpty(tagFile.Tag.FirstPerformer)) artist = tagFile.Tag.FirstPerformer;
                        duration = tagFile.Properties.Duration.TotalSeconds;
                    }
                    catch { }

                    string? lyricsContent = null;
                    if (File.Exists(lrcPath))
                    {
                        try { lyricsContent = File.ReadAllText(lrcPath); } catch { }
                    }

                    Catalog.Add(new SongItem
                    {
                        Title = title,
                        Artist = artist,
                        FilePath = file,
                        InstrumentalFilePath = instPath,
                        LyricsPath = File.Exists(lrcPath) ? lrcPath : null,
                        LyricsContent = lyricsContent,
                        DurationSeconds = duration,
                        IsVideo = ext == ".mp4" || ext == ".mkv"
                    });
                }
            }
        }

        public void PlaySong(SongItem song, string singer = "Tamu")
        {
            if (song == null) return;
            CurrentSong = song;
            CurrentSinger = singer;

            // Reset offset per song
            LyricOffsetSeconds = 0;
            _lastRenderedLine = null;
            CurrentWords.Clear();
            ActiveLineText = "♪ Menyiapkan Musik... ♪";
            NextLineText = "";

            // Parse lirik
            if (!string.IsNullOrEmpty(song.LyricsContent))
            {
                _parsedLines = LrcParser.Parse(song.LyricsContent);
            }
            else
            {
                _parsedLines = LrcParser.Parse($"[00:00.00]{song.Title} - {song.Artist}\n[00:02.50]Selamat Bernyanyi! 🎤");
            }

            // Jika user memilih vokal senyap dan AI Instrumental tersedia, coba putar AI Instrumental
            string audioToPlay = (VocalAttenuation >= 0.5f && song.HasAiInstrumental && !string.IsNullOrEmpty(song.InstrumentalFilePath))
                ? song.InstrumentalFilePath!
                : song.FilePath;

            if (!File.Exists(audioToPlay) && File.Exists(song.FilePath))
            {
                audioToPlay = song.FilePath;
            }

            bool loaded = _audioEngine.LoadSong(audioToPlay);
            if (!loaded && audioToPlay != song.FilePath && File.Exists(song.FilePath))
            {
                // Fallback ke file asli
                audioToPlay = song.FilePath;
                loaded = _audioEngine.LoadSong(audioToPlay);
            }

            if (loaded)
            {
                _audioEngine.Play();
                IsPlaying = true;
                TotalDuration = _audioEngine.GetDurationSeconds();
                _syncTimer.Start();
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"Gagal memutar audio:\n{Path.GetFileName(audioToPlay)}\n\nDetail BASS Audio Error: {_audioEngine.GetLastError()}\nPath: {audioToPlay}",
                    "Kesalahan Pemutaran Lagu",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        public async Task<bool> SeparateAudioFileWithAiAsync(string audioFilePath, Action<double, string>? progressCallback = null)
        {
            if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
                return false;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string toolExe = Path.Combine(baseDir, "tools", "OwnVocalRemover", "Example.Simplified.exe");
            if (!File.Exists(toolExe))
            {
                string altTool = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\tools\OwnVocalRemover\Example.Simplified.exe"));
                if (File.Exists(altTool)) toolExe = altTool;
            }

            if (!File.Exists(toolExe))
            {
                progressCallback?.Invoke(0, "Tool OwnVocalRemover belum ditemukan.");
                return false;
            }

            string outDir = Path.GetDirectoryName(audioFilePath) ?? baseDir;

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = toolExe,
                Arguments = $"\"{audioFilePath}\" \"{outDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = psi };
            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    double pct = 0;
                    if (e.Data.StartsWith("[PROGRESS]"))
                    {
                        var parts = e.Data.Split('-');
                        if (parts.Length > 0)
                        {
                            string pctStr = parts[0].Replace("[PROGRESS]", "").Replace("%", "").Trim().Replace(',', '.');
                            if (double.TryParse(pctStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
                            {
                                pct = Math.Clamp(val, 0.0, 100.0);
                            }
                        }
                    }
                    progressCallback?.Invoke(pct, e.Data);
                }
            };

            process.Start();
            try { process.PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal; } catch { }
            process.BeginOutputReadLine();
            await process.WaitForExitAsync();

            ScanDefaultSongsDirectory();
            return true;
        }

        public async Task<bool> SeparateSongWithAiAsync(SongItem song, Action<string>? progressCallback = null)
        {
            if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
                return false;

            return await SeparateAudioFileWithAiAsync(song.FilePath, (pct, status) => progressCallback?.Invoke(status));
        }

        public async Task<bool> AutoFetchLyricsForSongAsync(SongItem song)
        {
            if (song == null) return false;

            double duration = song.DurationSeconds > 0 ? song.DurationSeconds : 180;
            string? lrc = await _lyricsService.FetchFromLrclibAsync(song.Title, song.Artist, duration);
            if (!string.IsNullOrEmpty(lrc))
            {
                string dir = Path.GetDirectoryName(song.FilePath) ?? "";
                string lrcPath = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(song.FilePath)}.lrc");
                File.WriteAllText(lrcPath, lrc);
                song.LyricsPath = lrcPath;
                song.LyricsContent = lrc;

                if (CurrentSong == song)
                {
                    _parsedLines = LrcParser.Parse(lrc);
                }
                return true;
            }
            return false;
        }

        public void TogglePlayPause()
        {
            if (_audioEngine.IsPlaying)
            {
                _audioEngine.Pause();
                IsPlaying = false;
            }
            else if (CurrentSong != null)
            {
                _audioEngine.Play();
                IsPlaying = true;
                _syncTimer.Start();
            }
        }

        public void Stop()
        {
            _audioEngine.Stop();
            IsPlaying = false;
            CurrentPosition = 0;
            _syncTimer.Stop();
            ActiveLineText = "Musik Berhenti";
            CurrentWords.Clear();
        }

        public void Seek(double seconds)
        {
            _audioEngine.Seek(seconds);
            CurrentPosition = seconds;
        }

        public void AdjustOffset(double delta)
        {
            LyricOffsetSeconds += delta;
        }

        public void ResetOffset()
        {
            LyricOffsetSeconds = 0;
        }

        public void SetVocalMode(VocalChannelMode mode)
        {
            _audioEngine.SetVocalMode(mode);
        }

        public async Task SearchYouTubeAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            IsSearchingYouTube = true;
            YouTubeResults.Clear();

            try
            {
                var list = await _youtubeService.SearchAsync(query);
                foreach (var item in list) YouTubeResults.Add(item);
            }
            finally
            {
                IsSearchingYouTube = false;
            }
        }

        public async Task<string?> DownloadYouTubeSongAsync(YouTubeSearchResult item)
        {
            if (item == null || item.IsDownloading) return null;

            item.IsDownloading = true;
            item.IsCompleted = false;
            item.DownloadProgress = 0;
            item.StatusText = "Menyiapkan...";

            try
            {
                string songsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "songs");
                if (!Directory.Exists(songsDir))
                {
                    string altDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\songs");
                    if (Directory.Exists(altDir)) songsDir = Path.GetFullPath(altDir);
                    else Directory.CreateDirectory(songsDir);
                }

                // 1. TAHAP 1: Unduh audio & konversi ke WAV (Rentang progress: 0% - 25%)
                var downloadProgress = new Progress<double>(p =>
                {
                    double unduhPct = Math.Clamp(p * 100.0, 0.0, 100.0);
                    item.DownloadProgress = Math.Clamp(p * 25.0, 0.0, 25.0);
                    item.StatusText = $"⬇️ Unduh {unduhPct:F0}%";
                });

                item.StatusText = "⬇️ Mengunduh...";
                string savedPath = await _youtubeService.DownloadAudioAsync(item.Id, songsDir, downloadProgress);

                // 2. TAHAP 2: Lirik sudah otomatis diunduh di dalam DownloadAudioAsync (Rentang progress: 25% - 35%)
                item.DownloadProgress = 35.0;
                item.StatusText = "🎵 Lirik Siap";

                // 3. TAHAP 3: Pemisahan Vokal AI Otomatis (Rentang progress: 35% - 100%)
                item.StatusText = "✨ AI Vokal 0%";
                await SeparateAudioFileWithAiAsync(savedPath, (aiPct, msg) =>
                {
                    aiPct = Math.Clamp(aiPct, 0.0, 100.0);
                    double mappedProgress = 35.0 + (aiPct * 0.65); // 35% -> 100%
                    item.DownloadProgress = Math.Clamp(mappedProgress, 0.0, 99.0);
                    item.StatusText = $"✨ AI Vokal {aiPct:F0}%";
                });

                item.DownloadProgress = 100.0;
                item.StatusText = "✅ Selesai 100% (Siap Karaoke!)";
                item.IsCompleted = true;

                // Refresh katalog lagu lokal
                ScanDefaultSongsDirectory();

                return savedPath;
            }
            catch
            {
                item.StatusText = "❌ Gagal";
                throw;
            }
            finally
            {
                item.IsDownloading = false;
            }
        }

        private void OnSyncTick(object? sender, EventArgs e)
        {
            if (!_audioEngine.IsPlaying && !IsPlaying) return;

            double pos = _audioEngine.GetPositionSeconds();
            CurrentPosition = pos;

            var (current, next, words) = LrcParser.GetState(_parsedLines, pos, LyricOffsetSeconds);

            if (current != null)
            {
                ActiveLineText = current.Text;

                // Jika baris berubah atau jumlah kata berbeda, ganti seluruh koleksi kata secara bersih
                if (_lastRenderedLine != current || CurrentWords.Count != words.Count)
                {
                    _lastRenderedLine = current;
                    CurrentWords.Clear();
                    foreach (var w in words) CurrentWords.Add(w);
                }
                else
                {
                    // Update status kata secara in-place (INotifyPropertyChanged pada LyricWord memicu DataTrigger secara real-time)
                    for (int i = 0; i < words.Count && i < CurrentWords.Count; i++)
                    {
                        CurrentWords[i].Status = words[i].Status;
                    }
                }
            }
            else
            {
                _lastRenderedLine = null;
                CurrentWords.Clear();
                ActiveLineText = "♪ Musik Intro ♪";
            }

            NextLineText = next != null ? $"Selanjutnya: {next.Text}" : "";

            // Sync to Dual-Screen Singer Window
            OnSingerViewSync?.Invoke(CurrentSong, CurrentSinger, NextLineText, CurrentWords);
        }

        private void OnSongEnded()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying = false;
                if (Queue.Count > 0)
                {
                    var next = Queue[0];
                    Queue.RemoveAt(0);
                    PlaySong(next.Song, next.SingerName);
                }
            });
        }

        public void Dispose()
        {
            _syncTimer.Stop();
            _audioEngine.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
