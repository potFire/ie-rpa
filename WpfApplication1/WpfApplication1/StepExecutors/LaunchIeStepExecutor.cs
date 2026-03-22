using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class LaunchIeStepExecutor : IStepExecutor
    {
        private readonly IIeBrowserService _browserService;
        private readonly IVariableResolver _variableResolver;

        public LaunchIeStepExecutor(IIeBrowserService browserService, IVariableResolver variableResolver)
        {
            _browserService = browserService;
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.LaunchIe; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string url;
            step.Parameters.TryGetValue("url", out url);
            url = _variableResolver.ResolveString(url, context);

            var page = await _browserService.LaunchAsync(url, step.TimeoutMs);
            context.CurrentPage = page;
            context.CurrentBrowser = page;

            return StepExecutionResult.Success("IE 已启动。当前页面：" + page.Url);
        }
    }
}
