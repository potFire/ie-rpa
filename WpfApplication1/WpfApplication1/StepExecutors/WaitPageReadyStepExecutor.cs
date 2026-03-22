using System;
using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class WaitPageReadyStepExecutor : IStepExecutor
    {
        public StepType StepType
        {
            get { return StepType.WaitPageReady; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = context.CurrentPage as IIePage;
            if (page == null)
            {
                throw new InvalidOperationException("当前没有可等待的 IE 页面。");
            }

            await page.WaitForReadyAsync(step.TimeoutMs);
            return StepExecutionResult.Success("页面已完成加载。" + (string.IsNullOrWhiteSpace(page.Title) ? string.Empty : " 标题：" + page.Title));
        }
    }
}
