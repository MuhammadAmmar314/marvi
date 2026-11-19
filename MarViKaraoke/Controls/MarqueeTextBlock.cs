using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MarViKaraoke.Controls
{
    public enum MarqueeDirection
    {
        Left,
        Right
    }

    public class MarqueeTextBlock : Canvas
    {
        private readonly TextBlock _textBlock1;
        private readonly TextBlock _textBlock2;
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

        public static readonly DependencyProperty DirectionProperty =
            DependencyProperty.Register(nameof(Direction), typeof(MarqueeDirection), typeof(MarqueeTextBlock),
                new PropertyMetadata(MarqueeDirection.Right, OnTextOrVisualChanged));

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

        public MarqueeDirection Direction
        {
            get => (MarqueeDirection)GetValue(DirectionProperty);
            set => SetValue(DirectionProperty, value);
        }

        public MarqueeTextBlock()
        {
            ClipToBounds = true;
            Height = 22;

            _transform = new TranslateTransform();

            _textBlock1 = new TextBlock
            {
                RenderTransform = _transform,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                Foreground = Brushes.White
            };

            _textBlock2 = new TextBlock
            {
                RenderTransform = _transform,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                Foreground = Brushes.White,
                Visibility = Visibility.Collapsed
            };

            Children.Add(_textBlock1);
            Children.Add(_textBlock2);

            Loaded += (s, e) => UpdateAnimation();
            SizeChanged += (s, e) => UpdateAnimation();
        }

        private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarqueeTextBlock m)
            {
                var brush = (e.NewValue as Brush) ?? Brushes.White;
                if (m._textBlock1 != null) m._textBlock1.Foreground = brush;
                if (m._textBlock2 != null) m._textBlock2.Foreground = brush;
            }
        }

        private static void OnTextOrVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarqueeTextBlock m && m._textBlock1 != null && m._textBlock2 != null)
            {
                string text = m.Text ?? string.Empty;
                m._textBlock1.Text = text;
                m._textBlock2.Text = text;

                m._textBlock1.FontSize = m.FontSize;
                m._textBlock2.FontSize = m.FontSize;

                m._textBlock1.FontWeight = m.FontWeight;
                m._textBlock2.FontWeight = m.FontWeight;

                var brush = m.Foreground ?? Brushes.White;
                m._textBlock1.Foreground = brush;
                m._textBlock2.Foreground = brush;

                m.ToolTip = text;

                if (m.IsLoaded)
                {
                    m.UpdateAnimation();
                }
            }
        }

        public void UpdateAnimation()
        {
            if (!IsLoaded || ActualWidth <= 0 || _textBlock1 == null || _textBlock2 == null) return;

            try
            {
                _transform.BeginAnimation(TranslateTransform.XProperty, null);
                _transform.X = 0;

                _textBlock1.Measure(new Size(double.PositiveInfinity, ActualHeight > 0 ? ActualHeight : 30));
                double textWidth = _textBlock1.DesiredSize.Width;
                double containerWidth = ActualWidth;

                const double gap = 48.0;

                // Jika teks muat di dalam container, posisikan normal tanpa animasi
                if (textWidth <= containerWidth + 4)
                {
                    _textBlock2.Visibility = Visibility.Collapsed;
                    Canvas.SetLeft(_textBlock1, 0);
                    return;
                }

                _textBlock2.Visibility = Visibility.Visible;
                double totalCycleDistance = textWidth + gap;
                double seconds = Math.Max(3.5, totalCycleDistance / 35.0); // Kecepatan baca wajar (~35 px/detik)

                if (Direction == MarqueeDirection.Right)
                {
                    // Bergerak ke kanan terus: TextBlock 1 mulai di 0, TextBlock 2 berada di sebelah kirinya (-totalCycleDistance)
                    Canvas.SetLeft(_textBlock1, 0);
                    Canvas.SetLeft(_textBlock2, -totalCycleDistance);

                    var anim = new DoubleAnimation
                    {
                        From = 0,
                        To = totalCycleDistance,
                        Duration = TimeSpan.FromSeconds(seconds),
                        RepeatBehavior = RepeatBehavior.Forever
                    };

                    _transform.BeginAnimation(TranslateTransform.XProperty, anim);
                }
                else
                {
                    // Bergerak ke kiri terus: TextBlock 1 mulai di 0, TextBlock 2 berada di sebelah kanannya (+totalCycleDistance)
                    Canvas.SetLeft(_textBlock1, 0);
                    Canvas.SetLeft(_textBlock2, totalCycleDistance);

                    var anim = new DoubleAnimation
                    {
                        From = 0,
                        To = -totalCycleDistance,
                        Duration = TimeSpan.FromSeconds(seconds),
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
