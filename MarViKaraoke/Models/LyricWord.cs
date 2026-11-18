using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MarViKaraoke.Models
{
    public enum WordStatus
    {
        Future,
        Active,
        Passed
    }

    public class LyricWord : INotifyPropertyChanged
    {
        private string _text = string.Empty;
        private double _startTime;
        private double _endTime;
        private WordStatus _status = WordStatus.Future;

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged();
                }
            }
        }

        public double StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public double EndTime
        {
            get => _endTime;
            set
            {
                if (_endTime != value)
                {
                    _endTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Duration => Math.Max(0.05, EndTime - StartTime);

        public WordStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
