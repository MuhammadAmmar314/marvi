using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MarViKaraoke.Controls
{
    public class MarqueeTextBlock : Canvas
    {
        private readonly TextBlock _textBlock;
        private readonly TranslateTransform _transform;

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(MarqueeTextBlock),
                new PropertyMetadata(string.Empty, OnTextOrVisualChanged));

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(MarqueeTextBlock),
                new PropertyMetadata(13.0, OnTextOrVisualChanged));

        public static readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(MarqueeTextBlock),
                new PropertyMetadata(FontWeights.Bold, OnTextOrVisualChanged));

        public static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(MarqueeTextBlock),
                new PropertyMetadata(Brushes.White, OnForegroundChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public FontWeight FontWeight
        {
            get => (FontWeight)GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        public Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public MarqueeTextBlock()
        {
            ClipToBounds = true;
            Height = 22;

            _transform = new TranslateTransform();
            _textBlock = new TextBlock
            {
                RenderTransform = _transform,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                Foreground = Brushes.White
            };

            Children.Add(_textBlock);

            Loaded += (s, e) => UpdateAnimation();
            SizeChanged += (s, e) => UpdateAnimation();
        }

        private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarqueeTextBlock m && m._textBlock != null)
            {
                m._textBlock.Foreground = (e.NewValue as Brush) ?? Brushes.White;
            }
        }

        private static void OnTextOrVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarqueeTextBlock m && m._textBlock != null)
            {
                m._textBlock.Text = m.Text ?? string.Empty;
                m._textBlock.FontSize = m.FontSize;
                m._textBlock.FontWeight = m.FontWeight;
                m._textBlock.Foreground = m.Foreground ?? Brushes.White;
                m.ToolTip = m.Text;
                
                if (m.IsLoaded)
                {
                    m.UpdateAnimation();
                }
            }
        }

        public void UpdateAnimation()
        {
            if (!IsLoaded || ActualWidth <= 0 || _textBlock == null) return;

            try
            {
                _textBlock.Measure(new Size(double.PositiveInfinity, ActualHeight > 0 ? ActualHeight : 30));
                double textWidth = _textBlock.DesiredSize.Width;
                double containerWidth = ActualWidth;

                _transform.BeginAnimation(TranslateTransform.XProperty, null);
                _transform.X = 0;

                // Jika teks lebih panjang dari lebar tampilan, jalankan animasi marquee bolak-balik
                if (textWidth > containerWidth + 6)
                {
                    double distance = textWidth - containerWidth + 24;
                    double seconds = Math.Max(3.0, distance / 35.0); // Kecepatan baca wajar (~35 px/detik)

                    var anim = new DoubleAnimation
                    {
                        From = 0,
                        To = -distance,
                        Duration = TimeSpan.FromSeconds(seconds),
                        BeginTime = TimeSpan.FromSeconds(1.2), // Jeda awal agar pembaca bisa melihat kata pertama
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever
                    };

                    _transform.BeginAnimation(TranslateTransform.XProperty, anim);
                }
            }
            catch
            {
                // Pengaman jika context visual belum siap
            }
        }
    }
}
