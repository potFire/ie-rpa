using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class AttachIeStepExecutor : IStepExecutor
    {
        private readonly IIeBrowserService _browserService;

        public AttachIeStepExecutor(IIeBrowserService browserService)
        {
            _browserService = browserService;
        }

        public StepType StepType
        {
            get { return StepType.AttachIe; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = await _browserService.AttachAsync(step.TimeoutMs);
            context.CurrentPage = page;
            context.CurrentBrowser = page;
            return StepExecutionResult.Success("已附加到 IE 页面：" + page.Url);
        }
    }
}
