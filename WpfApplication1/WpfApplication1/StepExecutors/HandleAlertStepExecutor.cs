using System;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class HandleAlertStepExecutor : IStepExecutor
    {
        private readonly IDesktopInteractionService _desktopInteractionService;
        private readonly IVariableResolver _variableResolver;

        public HandleAlertStepExecutor(IDesktopInteractionService desktopInteractionService, IVariableResolver variableResolver)
        {
            _desktopInteractionService = desktopInteractionService;
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.HandleAlert; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string action;
            string buttonText;
            string titleContains;

            step.Parameters.TryGetValue("action", out action);
            step.Parameters.TryGetValue("buttonText", out buttonText);
            step.Parameters.TryGetValue("titleContains", out titleContains);

            action = _variableResolver.ResolveString(action, context);
            buttonText = _variableResolver.ResolveString(buttonText, context);
            titleContains = _variableResolver.ResolveString(titleContains, context);

            if (string.IsNullOrWhiteSpace(buttonText))
            {
                buttonText = ResolveDefaultButtonText(action);
            }

            var handled = _desktopInteractionService.TryHandleDialog(buttonText, titleContains, step.TimeoutMs);
            if (!handled)
            {
                return Task.FromResult(StepExecutionResult.Failure("在超时时间内未找到可处理的弹窗。"));
            }

            return Task.FromResult(StepExecutionResult.Success("弹窗已处理，按钮：" + buttonText));
        }

        private static string ResolveDefaultButtonText(string action)
        {
            if (string.Equals(action, "dismiss", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "cancel", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "no", StringComparison.OrdinalIgnoreCase))
            {
                return "取消";
            }

            return "确定";
        }
    }
}
