using System;

namespace WpfApplication1.Models
{
    public class SchedulerSettings
    {
        public SchedulerSettings()
        {
            ApplyPriority = true;
            MaxContinuousApplyCount = 3;
            MainLoopIntervalMs = 2000;
            QueryIntervalWhenNoApplyMs = 5000;
            ResumePromptOnStartup = true;
        }

        public string ApplyWorkflowId { get; set; }

        public string QueryWorkflowId { get; set; }

        public string ApprovalWorkflowId { get; set; }

        public bool ApplyPriority { get; set; }

        public int MaxContinuousApplyCount { get; set; }

        public int MainLoopIntervalMs { get; set; }

        public int QueryIntervalWhenNoApplyMs { get; set; }

        public bool ResumePromptOnStartup { get; set; }

        public SchedulerSettings Clone()
        {
            return (SchedulerSettings)MemberwiseClone();
        }

        public void Normalize()
        {
            MaxContinuousApplyCount = Math.Max(1, MaxContinuousApplyCount);
            MainLoopIntervalMs = Math.Max(500, MainLoopIntervalMs);
            QueryIntervalWhenNoApplyMs = Math.Max(1000, QueryIntervalWhenNoApplyMs);
        }
    }
}
