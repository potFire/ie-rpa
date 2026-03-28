using System;
using WpfApplication1.Enums;

namespace WpfApplication1.Models
{
    public class PublishedWorkflowRecord
    {
        public string WorkflowId { get; set; }

        public string WorkflowName { get; set; }

        public WorkflowType WorkflowType { get; set; }

        public string ApplicableRole { get; set; }

        public string SourcePath { get; set; }

        public string PublishedSnapshotPath { get; set; }

        public DateTime PublishedAt { get; set; }

        public string Version { get; set; }

        public string DisplayText
        {
            get
            {
                return string.Format("{0} | {1} | v{2} | {3:MM-dd HH:mm}",
                    string.IsNullOrWhiteSpace(WorkflowName) ? WorkflowId : WorkflowName,
                    GetWorkflowTypeDisplay(WorkflowType),
                    string.IsNullOrWhiteSpace(Version) ? "0.1.0" : Version,
                    PublishedAt);
            }
        }

        private static string GetWorkflowTypeDisplay(WorkflowType workflowType)
        {
            switch (workflowType)
            {
                case WorkflowType.Apply:
                    return "申请流程";
                case WorkflowType.Approval:
                    return "审批流程";
                case WorkflowType.Query:
                    return "查询流程";
                case WorkflowType.IntegratedScheduler:
                    return "调度编排模板";
                case WorkflowType.Subflow:
                    return "子流程";
                default:
                    return "通用流程";
            }
        }
    }
}

