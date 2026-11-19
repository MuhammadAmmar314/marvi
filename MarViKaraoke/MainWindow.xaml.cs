using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using MarViKaraoke.Core;
using MarViKaraoke.Models;
using MarViKaraoke.Services;
using MarViKaraoke.ViewModels;

namespace MarViKaraoke
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private SingerWindow? _singerWindow;

        public MainWindow()
        {
            InitializeComponent();

            _vm = new MainViewModel();
            this.DataContext = _vm;

            _vm.Queue.CollectionChanged += (s, e) => Dispatcher.Invoke(UpdateQueueBadge);
            UpdateQueueBadge();

            _vm.OnSingerViewSync += (song, singer, nextLine, words) =>
            {
                if (_singerWindow != null && _singerWindow.IsLoaded)
                {
                    _singerWindow.UpdateSync(song, singer, nextLine, words);
                }
            };

            Loaded += (s, e) => Task.Run(CheckUpdateSilentlyAsync);
        }

        private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) =>
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
        }
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _singerWindow?.Close();
            _vm.Dispose();
            this.Close();
        }

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            _vm.TogglePlayPause();
            BtnPlayPause.Content = _vm.IsPlaying ? "⏸" : "▶";
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _vm.Stop();
            BtnPlayPause.Content = "▶";
        }

        private void BtnReplay_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.CurrentSong != null)
                _vm.PlaySong(_vm.CurrentSong, _vm.CurrentSinger);
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Queue.Count > 0)
            {
                var next = _vm.Queue[0];
                _vm.Queue.RemoveAt(0);
                _vm.PlaySong(next.Song, next.SingerName);
                ShowToastNotification("▶ Memutar Lagu Berikutnya", $"\"{next.Song.Title}\" ({next.SingerName})", "🎵");
            }
            else
            {
                MessageBox.Show("Antrean lagu kosong.", "MarVi Karaoke", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Slider manual seek
        }

        // Pitch
        private void BtnPitchDown_Click(object sender, RoutedEventArgs e) => _vm.PitchSemitones--;
        private void BtnPitchUp_Click(object sender, RoutedEventArgs e) => _vm.PitchSemitones++;
        private void BtnPitchReset_Click(object sender, RoutedEventArgs e) => _vm.PitchSemitones = 0;

        // Offset
        private void BtnOffsetMinus_Click(object sender, RoutedEventArgs e) => _vm.AdjustOffset(-0.5);
        private void BtnOffsetPlus_Click(object sender, RoutedEventArgs e) => _vm.AdjustOffset(0.5);
        private void BtnOffsetReset_Click(object sender, RoutedEventArgs e) => _vm.ResetOffset();

        // Vocal Remover Toggle
        private void BtnToggleVocal_Click(object sender, RoutedEventArgs e)
        {
            _vm.VocalAttenuation = _vm.VocalAttenuation >= 0.5f ? 0f : 1.0f;
        }

        private void CmbVocalMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || CmbVocalMode == null) return;
            if (CmbVocalMode.SelectedIndex == 0) _vm.SetVocalMode(VocalChannelMode.RemoveVocalDsp);
            else if (CmbVocalMode.SelectedIndex == 1) _vm.SetVocalMode(VocalChannelMode.LeftOnlyMusic);
            else if (CmbVocalMode.SelectedIndex == 2) _vm.SetVocalMode(VocalChannelMode.RightOnlyVocal);
            else _vm.SetVocalMode(VocalChannelMode.Stereo);
        }

        // Singer Window / TV View
        private void BtnOpenSingerWindow_Click(object sender, RoutedEventArgs e)
        {
            if (_singerWindow == null || !_singerWindow.IsLoaded)
            {
                _singerWindow = new SingerWindow();
                _singerWindow.Show();
            }
            else
            {
                _singerWindow.Activate();
            }
        }

        private void SingerBadge_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Ubah nama penyanyi
        }

        // Folder & File Scanner
        private void BtnScanFolder_Click(object sender, RoutedEventArgs e)
        {
            _vm.ScanDefaultSongsDirectory();
            MessageBox.Show($"Memindai ulang folder songs. Ditemukan {_vm.Catalog.Count} file musik.", "MarVi Karaoke");
        }

        private async void BtnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Media Karaoke (*.mp3;*.wav;*.mp4;*.mkv)|*.mp3;*.wav;*.mp4;*.mkv|Semua File (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                string songsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "songs");
                if (!System.IO.Directory.Exists(songsDir))
                    System.IO.Directory.CreateDirectory(songsDir);

                var importedPaths = new List<string>();
                foreach (var file in dialog.FileNames)
                {
                    string dest = System.IO.Path.Combine(songsDir, System.IO.Path.GetFileName(file));
                    if (!System.IO.File.Exists(dest))
                        System.IO.File.Copy(file, dest, true);
                    importedPaths.Add(dest);
                }

                _vm.ScanDefaultSongsDirectory();

                // Otomatisasi Lirik & Pemisahan Vokal AI untuk lagu yang baru diimpor
                int successCount = 0;
                foreach (var path in importedPaths)
                {
                    var song = _vm.Catalog.FirstOrDefault(s => string.Equals(s.FilePath, path, StringComparison.OrdinalIgnoreCase));
                    if (song != null)
                    {
                        // 1. Ambil lirik sinkron otomatis
                        await _vm.AutoFetchLyricsForSongAsync(song);

                        // 2. Pemisahan vokal AI jika belum ada file instrumental
                        if (!song.HasAiInstrumental)
                        {
                            await RunAiSeparationWithProgressAsync(song, false);
                        }
                        successCount++;
                    }
                }

                ShowToastNotification("✅ Import Selesai", $"{successCount} lagu berhasil diimpor & diproses otomatis!", "🎵");
            }
        }

        // Song item buttons
        private void BtnSongPlay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SongItem song)
            {
                _vm.PlaySong(song, _vm.CurrentSinger);
                BtnPlayPause.Content = "⏸";
            }
        }

        private void BtnSongQueue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SongItem song)
            {
                var item = new QueueItem { Song = song, SingerName = _vm.CurrentSinger };
                _vm.Queue.Add(item);
                ShowToastNotification("✅ Ditambahkan ke Antrean", $"\"{song.Title}\" (Urutan #{_vm.Queue.Count})", "✅");
            }
        }

        private void BtnSongMoreActions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private static SongItem? GetSongFromMenu(object sender)
        {
            if (sender is MenuItem menuItem)
            {
                if (menuItem.DataContext is SongItem song)
                    return song;

                if (menuItem.Parent is ContextMenu cm && cm.PlacementTarget is FrameworkElement target && target.DataContext is SongItem targetSong)
                    return targetSong;
            }
            return null;
        }

        private async void MenuSongFetchLyrics_Click(object sender, RoutedEventArgs e)
        {
            var song = GetSongFromMenu(sender);
            if (song == null || _vm == null) return;

            ShowToastNotification("⏳ Mengunduh Lirik", $"Mencari lirik untuk '{song.Title}'...", "🎵");
            bool ok = await _vm.AutoFetchLyricsForSongAsync(song);
            if (ok)
            {
                ShowToastNotification("🎵 Lirik Sinkron", $"Lirik untuk '{song.Title}' berhasil didapatkan!", "🎵");
            }
            else
            {
                ShowToastNotification("ℹ️ Lirik Belum Tersedia", $"Lirik untuk '{song.Title}' belum ditemukan di LRCLIB.", "ℹ️");
            }
        }

        private async void MenuSongAiSeparate_Click(object sender, RoutedEventArgs e)
        {
            var song = GetSongFromMenu(sender);
            if (song != null)
            {
                await RunAiSeparationWithProgressAsync(song, true);
            }
        }

        private void MenuSongDelete_Click(object sender, RoutedEventArgs e)
        {
            var song = GetSongFromMenu(sender);
            if (song == null || _vm == null) return;

            var result = MessageBox.Show(
                $"Apakah Anda yakin ingin menghapus lagu ini dari koleksi?\n\nJudul: {song.Title}\nArtis: {song.Artist}\n\nFile audio dan lirik akan dihapus dari penyimpanan.",
                "Konfirmasi Hapus Lagu",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                string title = song.Title;
                bool ok = _vm.DeleteSong(song);
                if (ok)
                {
                    ShowToastNotification("🗑️ Lagu Dihapus", $"\"{title}\" berhasil dihapus dari koleksi.", "🗑️");
                }
                else
                {
                    MessageBox.Show("Gagal menghapus file lagu dari penyimpanan.", "Error Hapus Lagu", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnSongFetchLyrics_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            if (sender is Button btn && btn.DataContext is SongItem song)
            {
                btn.IsEnabled = false;
                btn.Content = "⏳";
                try
                {
                    bool ok = await _vm.AutoFetchLyricsForSongAsync(song);
                    if (ok)
                    {
                        ShowToastNotification("🎵 Lirik Sinkron", $"Lirik untuk '{song.Title}' berhasil didapatkan!", "🎵");
                    }
                    else
                    {
                        ShowToastNotification("ℹ️ Lirik Belum Tersedia", $"Lirik untuk '{song.Title}' belum ditemukan di LRCLIB.", "ℹ️");
                    }
                }
                finally
                {
                    btn.IsEnabled = true;
                    btn.Content = "🎵";
                }
            }
        }

        private async void BtnSongAiSeparate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SongItem song)
            {
                await RunAiSeparationWithProgressAsync(song, true);
            }
        }

        private async Task RunAiSeparationWithProgressAsync(SongItem song, bool showSuccessDialog = true)
        {
            if (song == null) return;

            OverlayAiProcessing.Visibility = Visibility.Visible;
            TxtAiSongTitle.Text = $"{song.Title} - {song.Artist}";
            PbAiProgress.Value = 0;
            TxtAiStatus.Text = "Menyiapkan AI model ONNX...";

            try
            {
                bool ok = await _vm.SeparateSongWithAiAsync(song, (status) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtAiStatus.Text = status;
                        if (status.StartsWith("[PROGRESS]"))
                        {
                            var parts = status.Split('-');
                            if (parts.Length > 0)
                            {
                                string pctStr = parts[0].Replace("[PROGRESS]", "").Replace("%", "").Trim().Replace(',', '.');
                                if (double.TryParse(pctStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
                                {
                                    PbAiProgress.Value = Math.Clamp(val, 0.0, 100.0);
                                }
                            }
                            if (parts.Length > 1)
                            {
                                TxtAiStatus.Text = parts[1].Trim();
                            }
                        }
                    });
                });

                if (ok)
                {
                    if (showSuccessDialog)
                    {
                        MessageBox.Show($"Pemisahan vokal AI untuk '{song.Title}' selesai!\nFile instrumental bersih siap diputar.", "MarVi Karaoke AI", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ShowToastNotification("✨ AI Vokal Selesai", $"Instrumental bersih untuk '{song.Title}' siap diputar.", "✨");
                    }
                }
            }
            catch (Exception ex)
            {
                if (showSuccessDialog)
                {
                    MessageBox.Show($"Gagal memisahkan vokal: {ex.Message}", "Error AI Separator", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    ShowToastNotification("❌ AI Gagal", $"Gagal memisahkan vokal '{song.Title}': {ex.Message}", "❌");
                }
            }
            finally
            {
                OverlayAiProcessing.Visibility = Visibility.Collapsed;
            }
        }

        private void SwitchRightTab(int tabIndex)
        {
            PanelCatalog.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            PanelQueue.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            PanelYouTube.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

            TabBtnCatalog.Style = (Style)FindResource(tabIndex == 0 ? "PrimaryActionBtn" : "NeonBtn");
            TabBtnQueue.Style = (Style)FindResource(tabIndex == 1 ? "PrimaryActionBtn" : "NeonBtn");
            TabBtnYouTube.Style = (Style)FindResource(tabIndex == 2 ? "PrimaryActionBtn" : "NeonBtn");
        }

        private void TabBtnCatalog_Click(object sender, RoutedEventArgs e) => SwitchRightTab(0);
        private void TabBtnQueue_Click(object sender, RoutedEventArgs e) => SwitchRightTab(1);
        private void TabBtnYouTube_Click(object sender, RoutedEventArgs e) => SwitchRightTab(2);

        private async void BtnYouTubeSearch_Click(object sender, RoutedEventArgs e)
        {
            string query = TxtYouTubeSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            TxtYouTubeSearch.IsEnabled = false;
            try
            {
                await _vm.SearchYouTubeAsync(query);
            }
            finally
            {
                TxtYouTubeSearch.IsEnabled = true;
            }
        }

        private void TxtYouTubeSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnYouTubeSearch_Click(sender, e);
            }
        }

        private async void BtnYouTubeDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is YouTubeSearchResult item)
            {
                if (item.IsCompleted)
                {
                    // Jika sudah selesai diproses, langsung arahkan ke koleksi lagu
                    SwitchRightTab(0);
                    var cleanTitle = LyricsService.CleanSongTitle(item.Title);
                    var matching = _vm.Catalog.FirstOrDefault(s => s.Title.Contains(cleanTitle, StringComparison.OrdinalIgnoreCase) || cleanTitle.Contains(s.Title, StringComparison.OrdinalIgnoreCase));
                    if (matching != null)
                    {
                        SongsListBox.SelectedItem = matching;
                        SongsListBox.ScrollIntoView(matching);
                    }
                    return;
                }

                try
                {
                    string? savedPath = await _vm.DownloadYouTubeSongAsync(item);
                    if (!string.IsNullOrEmpty(savedPath))
                    {
                        // Jika saat ini sedang bernyanyi, biarkan user tetap di tab pencarian agar bisa mencari lagu berikutnya
                        if (!_vm.IsPlaying)
                        {
                            SwitchRightTab(0);
                            var matchingSong = _vm.Catalog.FirstOrDefault(s => s.FilePath == savedPath);
                            if (matchingSong != null)
                            {
                                SongsListBox.SelectedItem = matchingSong;
                                SongsListBox.ScrollIntoView(matchingSong);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal memproses lagu YouTube: {ex.Message}", "Download & AI Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_vm == null || SongsListBox == null) return;
            string filter = TxtSearch.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filter))
            {
                SongsListBox.ItemsSource = _vm.Catalog;
            }
            else
            {
                SongsListBox.ItemsSource = _vm.Catalog.Where(s =>
                    s.Title.ToLower().Contains(filter) || s.Artist.ToLower().Contains(filter)).ToList();
            }
        }

        private void SongsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SongsListBox.SelectedItem is SongItem song)
            {
                _vm.PlaySong(song, _vm.CurrentSinger);
                BtnPlayPause.Content = "⏸";
            }
        }

        // SFX
        private void BtnApplause_Click(object sender, RoutedEventArgs e) { }
        private void BtnAirhorn_Click(object sender, RoutedEventArgs e) { }

        // Toast Notification System
        private DispatcherTimer? _toastTimer;

        public void ShowToastNotification(string title, string message, string icon = "✅")
        {
            Dispatcher.Invoke(() =>
            {
                TxtToastIcon.Text = icon;
                TxtToastTitle.Text = title;
                TxtToastMessage.Text = message;

                NotificationToast.BeginAnimation(UIElement.OpacityProperty, null);
                var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
                NotificationToast.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                _toastTimer?.Stop();
                _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.8) };
                _toastTimer.Tick += (s, e) =>
                {
                    _toastTimer.Stop();
                    var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(400));
                    NotificationToast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                };
                _toastTimer.Start();
            });
        }

        private void UpdateQueueBadge()
        {
            if (TabBtnQueue != null)
            {
                TabBtnQueue.Content = _vm.Queue.Count > 0 ? $"⏳ Antrean ({_vm.Queue.Count})" : "⏳ Antrean";
            }
            if (TxtEmptyQueueHint != null && QueueListBox != null)
            {
                TxtEmptyQueueHint.Visibility = _vm.Queue.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                QueueListBox.Visibility = _vm.Queue.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // Queue Actions: Putar Sekarang & Hapus dari Antrean
        private void BtnQueuePlayNow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is QueueItem item)
            {
                _vm.Queue.Remove(item);
                _vm.PlaySong(item.Song, item.SingerName);
                BtnPlayPause.Content = "⏸";
                ShowToastNotification("▶ Memutar Sekarang", $"\"{item.Song.Title}\" ({item.SingerName})", "🎵");
            }
        }

        private void BtnRemoveQueueItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is QueueItem item)
            {
                string title = item.Song.Title;
                _vm.Queue.Remove(item);
                ShowToastNotification("🗑️ Dihapus dari Antrean", $"\"{title}\" dikeluarkan dari antrean.", "🗑️");
            }
        }

        // Drag & Drop Queue Reordering
        private Point _dragStartPoint;
        private QueueItem? _draggedItem;

        private void QueueListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Jangan inisiasi drag jika user mengklik tombol (seperti Play atau Hapus)
            if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) != null)
            {
                _draggedItem = null;
                return;
            }

            _dragStartPoint = e.GetPosition(QueueListBox);

            var container = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (container != null && container.DataContext is QueueItem item)
            {
                _draggedItem = item;
            }
        }

        private void QueueListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                Point currentPos = e.GetPosition(QueueListBox);
                Vector diff = _dragStartPoint - currentPos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    try
                    {
                        var data = new DataObject("MarViQueueItem", _draggedItem);
                        DragDrop.DoDragDrop(QueueListBox, data, DragDropEffects.Move);
                    }
                    catch
                    {
                        // Pengaman drag drop
                    }
                    finally
                    {
                        _draggedItem = null;
                    }
                }
            }
        }

        private void QueueListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _draggedItem = null;
        }

        private void QueueListBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("MarViQueueItem"))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void QueueListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("MarViQueueItem"))
            {
                var droppedData = e.Data.GetData("MarViQueueItem") as QueueItem;
                if (droppedData != null)
                {
                    var hit = QueueListBox.InputHitTest(e.GetPosition(QueueListBox)) as DependencyObject;
                    var targetContainer = FindVisualParent<ListBoxItem>(hit);

                    int oldIndex = _vm.Queue.IndexOf(droppedData);
                    if (oldIndex >= 0)
                    {
                        int newIndex = -1;
                        if (targetContainer != null && targetContainer.DataContext is QueueItem targetItem)
                        {
                            newIndex = _vm.Queue.IndexOf(targetItem);
                        }
                        else
                        {
                            newIndex = _vm.Queue.Count - 1;
                        }

                        if (newIndex >= 0 && newIndex != oldIndex && newIndex < _vm.Queue.Count)
                        {
                            _vm.Queue.Move(oldIndex, newIndex);
                            ShowToastNotification("🔄 Urutan Diperbarui", $"\"{droppedData.Song.Title}\" pindah ke posisi #{newIndex + 1}", "↕");
                        }
                    }
                }
            }
            _draggedItem = null;
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;

                if (child is Visual || child is System.Windows.Media.Media3D.Visual3D)
                    child = VisualTreeHelper.GetParent(child);
                else
                    child = LogicalTreeHelper.GetParent(child);
            }
            return null;
        }

        // In-App Auto Updater (GitHub Releases)
        private readonly UpdateService _updateService = new UpdateService();
        private UpdateInfo? _currentUpdateInfo;

        private async Task CheckUpdateSilentlyAsync()
        {
            try
            {
                var info = await _updateService.CheckForUpdatesAsync();
                if (info.IsUpdateAvailable)
                {
                    Dispatcher.Invoke(() =>
                    {
                        BtnCheckUpdate.Content = $"✨ Update ({info.LatestVersion})";
                        BtnCheckUpdate.BorderBrush = (Brush)FindResource("AccentCyanBrush");
                        ShowToastNotification("✨ Pembaruan Tersedia", $"MarVi Karaoke {info.LatestVersion} telah dirilis di GitHub. Klik tombol Update!", "🔄");
                    });
                }
            }
            catch
            {
                // Pengaman silent check
            }
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            TxtGitHubRepo.Text = _updateService.GitHubRepo;
            OverlayUpdate.Visibility = Visibility.Visible;
            PanelUpdateProgress.Visibility = Visibility.Collapsed;
            await PerformUpdateCheckAsync();
        }

        private void BtnCloseUpdateModal_Click(object sender, RoutedEventArgs e)
        {
            OverlayUpdate.Visibility = Visibility.Collapsed;
        }

        private async void BtnRecheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            string newRepo = TxtGitHubRepo.Text.Trim();
            if (!string.IsNullOrEmpty(newRepo))
            {
                _updateService.SaveConfig(newRepo);
            }
            await PerformUpdateCheckAsync();
        }

        private async Task PerformUpdateCheckAsync()
        {
            BtnApplyUpdate.IsEnabled = false;
            BtnCancelUpdate.IsEnabled = true;
            TxtUpdateChangelog.Text = "Menghubungi server GitHub Releases untuk memeriksa rilis terbaru...";
            TxtUpdateSizeInfo.Text = string.Empty;

            var info = await _updateService.CheckForUpdatesAsync(TxtGitHubRepo.Text.Trim());
            _currentUpdateInfo = info;

            TxtCurrentVersion.Text = info.CurrentVersion;
            TxtLatestVersion.Text = info.LatestVersion;
            TxtUpdateChangelog.Text = info.Changelog;

            if (info.IsUpdateAvailable)
            {
                TxtUpdateSizeInfo.Text = string.IsNullOrEmpty(info.FormattedSize) ? "" : $"📦 Ukuran: {info.FormattedSize}";
                BtnApplyUpdate.IsEnabled = !string.IsNullOrEmpty(info.DownloadUrl);
                BtnApplyUpdate.Content = "🚀 Unduh & Perbarui";
            }
            else
            {
                BtnApplyUpdate.IsEnabled = false;
                BtnApplyUpdate.Content = "✅ Versi Terbaru";
                if (string.IsNullOrEmpty(info.Changelog) || info.Changelog.StartsWith("Tidak ada") || info.Changelog.Contains("Repositori"))
                {
                    if (!info.Changelog.Contains("Repositori"))
                    {
                        TxtUpdateChangelog.Text = $"Aplikasi Anda sudah menggunakan versi paling mutakhir ({info.CurrentVersion}).";
                    }
                }
            }
        }

        private async void BtnApplyUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUpdateInfo == null || string.IsNullOrEmpty(_currentUpdateInfo.DownloadUrl))
                return;

            BtnApplyUpdate.IsEnabled = false;
            BtnCancelUpdate.IsEnabled = false;
            PanelUpdateProgress.Visibility = Visibility.Visible;
            TxtUpdateStatus.Text = "Menyiapkan pengunduhan...";
            PbUpdateDownload.Value = 0;
            TxtUpdateProgressPct.Text = "0%";

            try
            {
                var progress = new Progress<double>(pct =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        PbUpdateDownload.Value = pct;
                        TxtUpdateProgressPct.Text = $"{pct:F0}%";
                        TxtUpdateStatus.Text = $"Mengunduh paket rilis... ({pct:F0}%)";
                    });
                });

                string tempZip = await _updateService.DownloadUpdateZipAsync(_currentUpdateInfo.DownloadUrl, progress);

                TxtUpdateStatus.Text = "Mengekstrak dan menyiapkan instalasi pembaruan...";
                await Task.Delay(500);

                UpdateService.InstallAndRestart(tempZip);
            }
            catch (Exception ex)
            {
                PanelUpdateProgress.Visibility = Visibility.Collapsed;
                BtnCancelUpdate.IsEnabled = true;
                BtnApplyUpdate.IsEnabled = true;
                MessageBox.Show($"Gagal memasang pembaruan otomatis: {ex.Message}\n\nAnda dapat mengunduh langsung dari GitHub Releases secara manual.", "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
