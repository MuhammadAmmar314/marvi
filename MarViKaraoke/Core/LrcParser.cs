using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MarViKaraoke.Models;

namespace MarViKaraoke.Core
{
    public class LrcParser
    {
        private static readonly Regex LineTimeRegex = new Regex(@"\[(\d{2}):(\d{2})(?:\.(\d{2,3}))?\]", RegexOptions.Compiled);
        private static readonly Regex DurTagRegex = new Regex(@"<dur:([\d\.]+)>|<([\d\.]+)s>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex WordTimeRegex = new Regex(@"<(\d{2}):(\d{2})(?:\.(\d{2,3}))?>([^<]+)", RegexOptions.Compiled);
        private static readonly Regex SyllableVowelsRegex = new Regex(@"[aiueoyéèê]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static int CountSyllables(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return 1;
            var clean = Regex.Replace(word, @"[^a-zA-Z0-9]", "");
            if (string.IsNullOrEmpty(clean)) return 1;
            var matches = SyllableVowelsRegex.Matches(clean);
            return Math.Max(1, matches.Count);
        }

        public static List<LyricLine> Parse(string rawLrc)
        {
            var lines = new List<LyricLine>();
            if (string.IsNullOrWhiteSpace(rawLrc)) return lines;

            var rawLines = rawLrc.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (var raw in rawLines)
            {
                var trimmed = raw.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("[ti:") || trimmed.StartsWith("[ar:") || trimmed.StartsWith("[al:"))
                    continue;

                double? explicitDuration = null;
                var durMatch = DurTagRegex.Match(trimmed);
                if (durMatch.Success)
                {
                    if (double.TryParse(durMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var dur) ||
                        double.TryParse(durMatch.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out dur))
                    {
                        explicitDuration = dur;
                    }
                    trimmed = DurTagRegex.Replace(trimmed, "").Trim();
                }

                // Check for Enhanced LRC word timestamps <mm:ss.xx>kata
                var explicitWords = new List<LyricWord>();
                var wordMatches = WordTimeRegex.Matches(trimmed);
                foreach (Match wm in wordMatches)
                {
                    int m = int.Parse(wm.Groups[1].Value);
                    int s = int.Parse(wm.Groups[2].Value);
                    int ms = 0;
                    if (wm.Groups[3].Success)
                    {
                        var msStr = wm.Groups[3].Value;
                        ms = msStr.Length == 2 ? int.Parse(msStr) * 10 : int.Parse(msStr);
                    }
                    double wTime = m * 60 + s + (ms / 1000.0);
                    string wText = wm.Groups[4].Value.Trim();
                    if (!string.IsNullOrEmpty(wText))
                    {
                        explicitWords.Add(new LyricWord { Text = wText, StartTime = wTime });
                    }
                }

                // Extract Line Timestamps [mm:ss.xx]
                var lineMatches = LineTimeRegex.Matches(trimmed);
                var cleanText = LineTimeRegex.Replace(trimmed, "");
                cleanText = Regex.Replace(cleanText, @"<[^>]+>", "").Trim();

                if (lineMatches.Count > 0)
                {
                    foreach (Match lm in lineMatches)
                    {
                        int min = int.Parse(lm.Groups[1].Value);
                        int sec = int.Parse(lm.Groups[2].Value);
                        int ms = 0;
                        if (lm.Groups[3].Success)
                        {
                            var msStr = lm.Groups[3].Value;
                            ms = msStr.Length == 2 ? int.Parse(msStr) * 10 : int.Parse(msStr);
                        }
                        double totalSec = min * 60 + sec + (ms / 1000.0);

                        lines.Add(new LyricLine
                        {
                            Time = totalSec,
                            Text = string.IsNullOrEmpty(cleanText) ? "♪ ♪ ♪" : cleanText,
                            ExplicitDuration = explicitDuration,
                            Words = explicitWords.Count > 0 ? explicitWords : new List<LyricWord>()
                        });
                    }
                }
            }

            // Sort lines chronologically
            lines = lines.OrderBy(l => l.Time).ToList();

            // Calculate natural singing durations and word distribution
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                double nextTime = (i < lines.Count - 1) ? lines[i + 1].Time : (line.Time + 4.5);
                double gap = Math.Max(0.6, nextTime - line.Time);

                if (line.ExplicitDuration.HasValue && line.ExplicitDuration.Value > 0)
                {
                    line.Duration = Math.Min(gap, line.ExplicitDuration.Value);
                }
                else
                {
                    var rawWords = line.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int charCount = line.Text.Replace(" ", "").Length;
                    double estimatedVocal = Math.Max(1.4, (charCount * 0.11) + (rawWords.Length * 0.22));

                    if (gap > estimatedVocal + 0.6)
                        line.Duration = Math.Max(1.4, Math.Min(gap - 0.6, estimatedVocal));
                    else
                        line.Duration = Math.Max(1.0, gap * 0.90);
                }

                // If explicit words are not provided, distribute words based on syllable weights
                if (line.Words.Count == 0)
                {
                    var rawWords = line.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (rawWords.Length == 0)
                    {
                        line.Words.Add(new LyricWord { Text = line.Text, StartTime = line.Time, EndTime = line.Time + line.Duration });
                    }
                    else
                    {
                        var weights = rawWords.Select(CountSyllables).ToList();
                        int totalWeight = Math.Max(1, weights.Sum());
                        double curTime = line.Time;

                        for (int w = 0; w < rawWords.Length; w++)
                        {
                            double wDur = (weights[w] / (double)totalWeight) * line.Duration;
                            line.Words.Add(new LyricWord
                            {
                                Text = rawWords[w],
                                StartTime = curTime,
                                EndTime = curTime + wDur
                            });
                            curTime += wDur;
                        }
                    }
                }
                else
                {
                    for (int w = 0; w < line.Words.Count; w++)
                    {
                        if (w < line.Words.Count - 1)
                            line.Words[w].EndTime = line.Words[w + 1].StartTime;
                        else
                            line.Words[w].EndTime = line.Time + line.Duration;
                    }
                }
            }

            return lines;
        }

        public static (LyricLine? current, LyricLine? next, List<LyricWord> words) GetState(List<LyricLine> lines, double currentTime, double offsetSeconds = 0)
        {
            double effectiveTime = Math.Max(0, currentTime + offsetSeconds);
            if (lines == null || lines.Count == 0)
                return (null, null, new List<LyricWord>());

            int foundIdx = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (effectiveTime >= lines[i].Time)
                    foundIdx = i;
                else
                    break;
            }

            // KASUS 1: Sebelum baris pertama (Intro musik)
            if (foundIdx == -1)
            {
                var firstLine = lines[0];
                double timeUntilFirst = firstLine.Time - effectiveTime;

                // Tampilkan lirik baris pertama 3 detik sebelum vokal mulai agar penyanyi bisa membaca dan bersiap!
                if (timeUntilFirst <= 3.0)
                {
                    foreach (var w in firstLine.Words) w.Status = WordStatus.Future;
                    var upcoming = lines.Count > 1 ? lines[1] : null;
                    return (firstLine, upcoming, firstLine.Words);
                }
                else
                {
                    return (null, firstLine, new List<LyricWord>());
                }
            }

            var current = lines[foundIdx];
            var next = (foundIdx < lines.Count - 1) ? lines[foundIdx + 1] : null;

            // KASUS 2: Baris saat ini sudah selesai, dan baris berikutnya akan mulai dalam <= 1.5 detik
            // Lakukan roll-over awal ke baris berikutnya agar penyanyi memiliki waktu ancang-ancang
            if (next != null && effectiveTime >= current.EndTime)
            {
                double timeUntilNext = next.Time - effectiveTime;
                if (timeUntilNext <= 1.5)
                {
                    foreach (var w in next.Words) w.Status = WordStatus.Future;
                    var afterNext = (foundIdx + 2 < lines.Count) ? lines[foundIdx + 2] : null;
                    return (next, afterNext, next.Words);
                }
            }

            // Update word statuses untuk baris aktif
            foreach (var w in current.Words)
            {
                if (effectiveTime >= w.EndTime)
                    w.Status = WordStatus.Passed;
                else if (effectiveTime >= w.StartTime && effectiveTime < w.EndTime)
                    w.Status = WordStatus.Active;
                else
                    w.Status = WordStatus.Future;
            }

            return (current, next, current.Words);
        }
    }
}
