using System;

namespace WpfApplication1.Models
{
    public class RunHistoryItem
    {
        public string WorkflowName { get; set; }

        public string Mode { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime? EndedAt { get; set; }

        public string Result { get; set; }

        public TimeSpan Duration { get; set; }

        public string ErrorSummary { get; set; }

        public string DurationText
        {
            get
            {
                var duration = EndedAt.HasValue ? EndedAt.Value - StartedAt : Duration;
                return duration.TotalSeconds < 1
                    ? string.Format("{0} ms", Math.Max(0, (int)duration.TotalMilliseconds))
                    : string.Format("{0:F1} s", duration.TotalSeconds);
            }
        }
    }
}
