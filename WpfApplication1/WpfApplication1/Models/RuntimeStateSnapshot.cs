using System;

namespace WpfApplication1.Models
{
    public class RuntimeStateSnapshot
    {
        public string CurrentWorkflowName { get; set; }

        public string CurrentWorkflowPath { get; set; }

        public string CurrentMode { get; set; }

        public string CurrentStepId { get; set; }

        public string CurrentStepName { get; set; }

        public string CurrentWindowTitle { get; set; }

        public string CurrentPageUrl { get; set; }

        public string FramePathDisplay { get; set; }

        public int FrameDepth { get; set; }

        public string CurrentObject { get; set; }

        public string RecentErrorSummary { get; set; }

        public DateTime? LastUpdatedAt { get; set; }

        public RuntimeStateSnapshot Clone()
        {
            return (RuntimeStateSnapshot)MemberwiseClone();
        }
    }
}
