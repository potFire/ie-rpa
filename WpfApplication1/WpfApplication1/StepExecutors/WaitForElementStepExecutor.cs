using System;
using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Selectors;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class WaitForElementStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public WaitForElementStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.WaitForElement; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = context.CurrentPage as IIePage;
            if (page == null)
            {
                throw new InvalidOperationException("当前没有可用的 IE 页面，无法等待元素出现。");
            }

            string rawSelector;
            string rawPollInterval;
            step.Parameters.TryGetValue("selector", out rawSelector);
            step.Parameters.TryGetValue("pollIntervalMs", out rawPollInterval);

            var selectorText = _variableResolver.ResolveString(rawSelector, context);
            var pollIntervalMs = ResolvePollInterval(rawPollInterval);
            if (string.IsNullOrWhiteSpace(selectorText))
            {
                return StepExecutionResult.Failure("未配置等待元素的 selector。");
            }

            var selector = SelectorParser.Parse(selectorText);
            var timeoutMs = step.TimeoutMs > 0 ? step.TimeoutMs : 10000;
            var startedAt = DateTime.UtcNow;

            while ((DateTime.UtcNow - startedAt).TotalMilliseconds < timeoutMs)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                try
                {
                    page.FindElement(selector);
                    return StepExecutionResult.Success("目标元素已出现：" + selectorText);
                }
                catch (InvalidOperationException)
                {
                }

                await Task.Delay(pollIntervalMs, context.CancellationToken);
            }

            return StepExecutionResult.Failure("等待元素出现超时：" + selectorText);
        }

        private static int ResolvePollInterval(string rawPollInterval)
        {
            int pollIntervalMs;
            if (int.TryParse(rawPollInterval, out pollIntervalMs) && pollIntervalMs > 0)
            {
                return pollIntervalMs;
            }

            return 500;
        }
    }
}