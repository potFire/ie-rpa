using System;
using WpfApplication1.Enums;

namespace WpfApplication1.Models
{
    public class WorkflowLogRunItem
    {
        public string WorkflowId { get; set; }

        public string WorkflowName { get; set; }

        public WorkflowType WorkflowType { get; set; }

        public string WorkflowPath { get; set; }

        public string RunId { get; set; }

        public string RunName { get; set; }

        public string RunMode { get; set; }

        public string Result { get; set; }

        public string Summary { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime? EndedAt { get; set; }

        public string LogFilePath { get; set; }

        public string WorkflowTypeText
        {
            get { return ToWorkflowTypeText(WorkflowType); }
        }

        public string DisplayText
        {
            get
            {
                return string.Format("{0:MM-dd HH:mm:ss} | {1} | {2}",
                    StartedAt,
                    string.IsNullOrWhiteSpace(RunName) ? (string.IsNullOrWhiteSpace(RunMode) ? "执行批次" : RunMode) : RunName,
                    string.IsNullOrWhiteSpace(Result) ? "运行中" : Result);
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
