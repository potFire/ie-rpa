using System;

namespace WpfApplication1.Models
{
    public class TaskSummarySnapshot
    {
        public int PendingApplyCount { get; set; }

        public int PendingApprovalCount { get; set; }

        public int PendingQueryCount { get; set; }

        public int PendingResumeCount { get; set; }

        public int CompletedTodayCount { get; set; }

        public int FailedTodayCount { get; set; }

        public bool HasPendingTask { get; set; }

        public string SuggestedAction { get; set; }

        public DateTime? LastRefreshAt { get; set; }
    }
}
