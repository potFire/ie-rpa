using System;

namespace WpfApplication1.Models
{
    public class LocalSchedulerState
    {
        public string LastMode { get; set; }

        public int ContinuousApplyCount { get; set; }

        public bool HasPendingApply { get; set; }

        public bool HasPendingQuery { get; set; }

        public bool HasPendingUpload { get; set; }

        public string LastWorkflowPath { get; set; }

        public string LastStepId { get; set; }

        public DateTime? LastRunAt { get; set; }

        public string LastError { get; set; }
    }
}
