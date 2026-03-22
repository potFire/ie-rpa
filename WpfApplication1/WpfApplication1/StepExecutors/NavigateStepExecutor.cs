using System;
using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class NavigateStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public NavigateStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.Navigate; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = context.CurrentPage as IIePage;
            if (page == null)
            {
                throw new InvalidOperationException("当前没有可导航的 IE 页面，请先执行启动 IE 或附加 IE。");
            }

            string url;
            step.Parameters.TryGetValue("url", out url);
            url = _variableResolver.ResolveString(url, context);
            await page.NavigateAsync(url, step.TimeoutMs);
            context.CurrentPage = page;
            return StepExecutionResult.Success("页面已打开：" + page.Url);
        }
    }
}
