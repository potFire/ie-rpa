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

            string framePath;
            step.Parameters.TryGetValue("framePath", out framePath);
            framePath = _variableResolver.ResolveString(framePath, context);
            context.CurrentPage = page.GetFramePage(framePath);
            return Task.FromResult(StepExecutionResult.Success(string.IsNullOrWhiteSpace(framePath) ? "已回到根页面。" : "已切换到 frame：" + framePath));
        }
    }
}
