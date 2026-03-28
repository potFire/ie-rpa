using System;
using WpfApplication1.Enums;

namespace WpfApplication1.Models
{
    public class WorkflowLogWorkflowItem
    {
        public string WorkflowId { get; set; }

        public string WorkflowName { get; set; }

        public WorkflowType WorkflowType { get; set; }

        public DateTime? LastRunAt { get; set; }

        public string LastResult { get; set; }

        public int RunCount { get; set; }

        public string WorkflowTypeText
        {
            get { return ToWorkflowTypeText(WorkflowType); }
        }

        public string DisplayText
        {
            get
            {
                return string.Format("{0} | {1} | {2}",
                    string.IsNullOrWhiteSpace(WorkflowName) ? WorkflowId : WorkflowName,
                    WorkflowTypeText,
                    LastRunAt.HasValue ? LastRunAt.Value.ToString("MM-dd HH:mm") : "无记录");
            }
        }

        private static string ToWorkflowTypeText(WorkflowType workflowType)
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
