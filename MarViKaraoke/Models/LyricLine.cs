using System.Collections.Generic;

namespace MarViKaraoke.Models
{
    public class LyricLine
    {
        public double Time { get; set; }
        public string Text { get; set; } = string.Empty;
        public double Duration { get; set; } = 4.0;
        public double? ExplicitDuration { get; set; }
        public List<LyricWord> Words { get; set; } = new List<LyricWord>();

        public double EndTime => Time + Duration;

        public bool IsActive(double currentTime)
        {
            return currentTime >= Time && currentTime < EndTime;
        }

        public bool IsPassed(double currentTime)
        {
            return currentTime >= EndTime;
        }
    }
}
