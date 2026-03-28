using System;
using WpfApplication1.Enums;

namespace WpfApplication1.Models
{
    public class ExecutionLogEntry
    {
        public DateTime Timestamp { get; set; }

        public LogLevel Level { get; set; }

        public string WorkflowId { get; set; }

        public string WorkflowName { get; set; }

        public WorkflowType? WorkflowType { get; set; }

        public string RunId { get; set; }

        public string RunName { get; set; }

        public string RunMode { get; set; }

        public string StepId { get; set; }

        public string StepName { get; set; }

        public StepType? StepType { get; set; }

        public int Attempt { get; set; }

        public int MaxAttempts { get; set; }

        public string Status { get; set; }

        public string Message { get; set; }

        public string ScreenshotPath { get; set; }

        public string CurrentPageUrl { get; set; }

        public string CurrentWindowTitle { get; set; }

        public string FramePathDisplay { get; set; }

        public string CurrentObject { get; set; }

        public string ExceptionType { get; set; }

        public string FormattedLine { get; set; }
    }
}
