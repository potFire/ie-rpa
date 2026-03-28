using System;
using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class SwitchFrameStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public SwitchFrameStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.SwitchFrame; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = context.CurrentPage as IIePage;
            if (page == null)
            {
                throw new InvalidOperationException("当前没有可切换的 IE 页面。");
            }

            string action;
            string selectorText;
            string framePath;
            step.Parameters.TryGetValue("action", out action);
            step.Parameters.TryGetValue("selector", out selectorText);
            step.Parameters.TryGetValue("framePath", out framePath);

            action = _variableResolver.ResolveString(action, context);
            selectorText = _variableResolver.ResolveString(selectorText, context);
            framePath = _variableResolver.ResolveString(framePath, context);

            if (string.IsNullOrWhiteSpace(action) && !string.IsNullOrWhiteSpace(framePath))
            {
                context.CurrentPage = page.GetFramePage(framePath);
                return Task.FromResult(StepExecutionResult.Success(string.IsNullOrWhiteSpace(framePath) ? "已回到根页面。" : "已切换到 frame：" + framePath));
            }

            if (string.Equals(action, "parent", StringComparison.OrdinalIgnoreCase))
            {
                context.CurrentPage = page.GetParentFramePage();
                return Task.FromResult(StepExecutionResult.Success("已切回父级 iframe。"));
            }

            if (string.Equals(action, "root", StringComparison.OrdinalIgnoreCase))
            {
                context.CurrentPage = page.GetRootPage();
                return Task.FromResult(StepExecutionResult.Success("已切回根页面。"));
            }

            if (string.IsNullOrWhiteSpace(selectorText))
            {
                return Task.FromResult(StepExecutionResult.Failure("未配置 iframe selector。"));
            }

            var selector = WpfApplication1.Selectors.SelectorParser.Parse(selectorText);
            context.CurrentPage = page.EnterFrame(selector);
            return Task.FromResult(StepExecutionResult.Success("已进入目标 iframe。"));
        }
    }
}