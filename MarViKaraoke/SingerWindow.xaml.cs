using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MarViKaraoke.Models;

namespace MarViKaraoke
{
    public partial class SingerWindow : Window
    {
        private bool _isFullscreen = false;

        public SingerWindow()
        {
            InitializeComponent();
            MoveToSecondaryMonitorIfAvailable();
        }

        private void MoveToSecondaryMonitorIfAvailable()
        {
            // Auto-deteksi monitor kedua (misal TV yang terhubung via HDMI)
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (screens.Length > 1)
            {
                var secondary = screens[1];
                this.Left = secondary.WorkingArea.Left;
                this.Top = secondary.WorkingArea.Top;
                this.Width = secondary.WorkingArea.Width;
                this.Height = secondary.WorkingArea.Height;
                this.WindowState = WindowState.Maximized;
            }
        }

        public void UpdateSync(SongItem? song, string singer, string nextLine, ObservableCollection<LyricWord> words)
        {
            if (song != null)
            {
                TxtSongTitle.Text = song.Title;
                TxtArtist.Text = song.Artist;
            }
            TxtSinger.Text = $"🎤 {singer}";
            TxtNextLyric.Text = string.IsNullOrEmpty(nextLine) ? "" : nextLine;
            WordsItemsControl.ItemsSource = words;
            if (words == null || words.Count == 0)
            {
                TxtIntroStatus.Visibility = Visibility.Visible;
                WordsItemsControl.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtIntroStatus.Visibility = Visibility.Collapsed;
                WordsItemsControl.Visibility = Visibility.Visible;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11 || e.Key == Key.F)
            {
                ToggleFullscreen();
            }
        }

        private void ToggleFullscreen()
        {
            _isFullscreen = !_isFullscreen;
            if (_isFullscreen)
            {
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.WindowState = WindowState.Normal;
            }
        }
    }
}
