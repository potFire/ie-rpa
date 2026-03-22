using System;
using WpfApplication1.Enums;

namespace WpfApplication1.Models
{
    public class ExecutionLogEntry
    {
        public DateTime Timestamp { get; set; }

        public LogLevel Level { get; set; }

        public string StepId { get; set; }

        public string StepName { get; set; }

        public string Message { get; set; }

        public string ScreenshotPath { get; set; }
    }
}
