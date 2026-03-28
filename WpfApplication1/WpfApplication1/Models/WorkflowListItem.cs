using System;
using WpfApplication1.Enums;

namespace WpfApplication1.Models
{
    public class WorkflowListItem
    {
        public string WorkflowId { get; set; }

        public string Name { get; set; }

        public WorkflowType WorkflowType { get; set; }

        public string Description { get; set; }

        public string ApplicableRole { get; set; }

        public string SourcePath { get; set; }

        public string Version { get; set; }

        public DateTime? LastModifiedAt { get; set; }

        public bool IsPublished { get; set; }

        public DateTime? PublishedAt { get; set; }

        public string PublishedVersion { get; set; }

        public string WorkflowTypeDisplay
        {
            get { return GetWorkflowTypeDisplay(WorkflowType); }
        }

        public string PublishedDisplay
        {
            get
            {
                if (!IsPublished)
                {
                    return "未发布";
                }

                return string.Format("已发布 v{0} | {1:yyyy-MM-dd HH:mm}",
                    string.IsNullOrWhiteSpace(PublishedVersion) ? "0.1.0" : PublishedVersion,
                    PublishedAt ?? DateTime.Now);
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
