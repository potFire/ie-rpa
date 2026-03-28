using System;
using System.Collections.ObjectModel;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public class WorkflowTemplateFactory : IWorkflowTemplateFactory
    {
        public WorkflowDefinition CreateWorkflow(WorkflowCreateRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            var workflow = new WorkflowDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(request.Name) ? "未命名流程" : request.Name.Trim(),
                Description = request.Description ?? string.Empty,
                ApplicableRole = request.ApplicableRole ?? string.Empty,
                WorkflowType = request.WorkflowType,
                Version = "0.1.0",
                LastModifiedAt = DateTime.Now,
                Steps = new ObservableCollection<WorkflowStep>()
            };

            switch (request.WorkflowType)
            {
                case WorkflowType.Apply:
                    workflow.Steps.Add(CreateStep(StepType.HttpGetData, "拉取申请任务", "url", "http://localhost/api/apply"));
                    workflow.Steps.Add(CreateStep(StepType.UpdateBusinessState, "写入业务状态", "stage", "Fetched"));
                    workflow.Steps.Add(CreateStep(StepType.WriteLog, "申请主干占位", "message", "在这里继续配置申请输入、提交和成功判断步骤。"));
                    break;
                case WorkflowType.Approval:
                    workflow.Steps.Add(CreateStep(StepType.PageListLoop, "审批列表扫描", "mode", "approve"));
                    workflow.Steps.Add(CreateStep(StepType.WriteLog, "审批处理占位", "message", "在这里继续补充审批详情页、通过和返回逻辑。"));
                    break;
                case WorkflowType.Query:
                    workflow.Steps.Add(CreateStep(StepType.PageListLoop, "查询列表扫描", "mode", "queryReport"));
                    workflow.Steps.Add(CreateStep(StepType.QueryAndExportReport, "抓取报告并上传", "saveDirectory", "${ReportDirectory}"));
                    break;
                case WorkflowType.IntegratedScheduler:
                    workflow.Steps.Add(CreateStep(StepType.WriteLog, "调度编排模板", "message", "该类型用于展示调度编排模板，真实统一调度仍由应用级调度服务执行。"));
                    break;
                case WorkflowType.Subflow:
                    workflow.Steps.Add(CreateStep(StepType.WriteLog, "子流程占位", "message", "在这里设计可复用的子流程骨架。"));
                    break;
                default:
                    workflow.Steps.Add(CreateStep(StepType.WriteLog, "空白流程", "message", "从这里开始搭建新的自动化主干。"));
                    break;
            }

            workflow.EnsureCanvasLayout();
            return workflow;
        }

        private static WorkflowStep CreateStep(StepType stepType, string name, string parameterKey, string parameterValue)
        {
            var step = new WorkflowStep
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                StepType = stepType,
                Parameters = new StepParameterBag()
            };

            if (!string.IsNullOrWhiteSpace(parameterKey))
            {
                step.Parameters[parameterKey] = parameterValue ?? string.Empty;
            }

            return step;
        }
    }
}
