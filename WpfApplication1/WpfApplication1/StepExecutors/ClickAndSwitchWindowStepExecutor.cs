using System;
using System.Linq;
using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Selectors;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class ClickAndSwitchWindowStepExecutor : IStepExecutor
    {
        private readonly IIeBrowserService _browserService;
        private readonly IVariableResolver _variableResolver;

        public ClickAndSwitchWindowStepExecutor(IIeBrowserService browserService, IVariableResolver variableResolver)
        {
            _browserService = browserService;
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.ClickAndSwitchWindow; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var currentPage = context.CurrentPage as IIePage;
            if (currentPage == null)
            {
                throw new InvalidOperationException("当前没有可用的 IE 页面，无法执行点击并切换窗口。");
            }

            string rawSelector;
            string rawTargetTitle;
            string rawMatchMode;
            string rawPollInterval;
            string rawExcludeCurrent;
            step.Parameters.TryGetValue("clickSelector", out rawSelector);
            step.Parameters.TryGetValue("targetWindowTitle", out rawTargetTitle);
            step.Parameters.TryGetValue("matchMode", out rawMatchMode);
            step.Parameters.TryGetValue("pollIntervalMs", out rawPollInterval);
            step.Parameters.TryGetValue("excludeCurrent", out rawExcludeCurrent);

            var selectorText = _variableResolver.ResolveString(rawSelector, context);
            var targetWindowTitle = _variableResolver.ResolveString(rawTargetTitle, context);
            var matchMode = NormalizeMatchMode(_variableResolver.ResolveString(rawMatchMode, context));
            var pollIntervalMs = ResolvePollInterval(rawPollInterval);
            var excludeCurrent = ParseBoolean(rawExcludeCurrent, true);
            if (string.IsNullOrWhiteSpace(selectorText))
            {
                return StepExecutionResult.Failure("未配置点击目标 clickSelector。");
            }

            if (string.IsNullOrWhiteSpace(targetWindowTitle))
            {
                return StepExecutionResult.Failure("未配置目标窗口标题。");
            }

            var selector = SelectorParser.Parse(selectorText);
            currentPage.FindElement(selector).Click();

            var timeoutMs = step.TimeoutMs > 0 ? step.TimeoutMs : 10000;
            var startedAt = DateTime.UtcNow;
            var lastPages = _browserService.GetAllPages();
            while ((DateTime.UtcNow - startedAt).TotalMilliseconds < timeoutMs)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                lastPages = _browserService.GetAllPages();
                var targetPage = lastPages
                    .Where(page => page != null)
                    .Where(page => !excludeCurrent || !IsSamePage(page, currentPage))
                    .FirstOrDefault(page => IsWindowTitleMatch(page.Title, targetWindowTitle, matchMode));
                if (targetPage != null)
                {
                    targetPage.Activate();
                    await targetPage.WaitForReadyAsync(Math.Min(timeoutMs, 5000));
                    context.CurrentPage = targetPage;
                    context.CurrentBrowser = targetPage;
                    return StepExecutionResult.Success("已切换到目标窗口：" + targetPage.Title);
                }

                await Task.Delay(pollIntervalMs, context.CancellationToken);
            }

            var candidates = string.Join(" | ", lastPages.Where(page => page != null).Take(5).Select(page => string.Format("Title={0}, Url={1}", SafeValue(page.Title), SafeValue(page.Url))).ToArray());
            return StepExecutionResult.Failure("点击后未找到匹配标题的新窗口：" + targetWindowTitle + "。候选窗口：" + (string.IsNullOrWhiteSpace(candidates) ? "(none)" : candidates));
        }

        private static bool IsSamePage(IIePage left, IIePage right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            var sameTitle = string.Equals(left.Title ?? string.Empty, right.Title ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            var sameUrl = string.Equals(left.Url ?? string.Empty, right.Url ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (!sameTitle || !sameUrl)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(left.BrowserIdentityKey)
                && !string.IsNullOrWhiteSpace(right.BrowserIdentityKey))
            {
                return string.Equals(left.BrowserIdentityKey, right.BrowserIdentityKey, StringComparison.OrdinalIgnoreCase);
            }

            return left.WindowHandle == right.WindowHandle;
        }

        private static bool IsWindowTitleMatch(string title, string expectedTitle, string matchMode)
        {
            var actual = title ?? string.Empty;
            var expected = expectedTitle ?? string.Empty;
            switch (matchMode)
            {
                case "exact":
                    return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
                case "startswith":
                    return actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase);
                case "endswith":
                    return actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase);
                default:
                    return actual.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private static string NormalizeMatchMode(string rawMatchMode)
        {
            if (string.IsNullOrWhiteSpace(rawMatchMode))
            {
                return "contains";
            }

            switch (rawMatchMode.Trim().ToLowerInvariant())
            {
                case "exact":
                case "startswith":
                case "endswith":
                    return rawMatchMode.Trim().ToLowerInvariant();
                default:
                    return "contains";
            }
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

        private static bool ParseBoolean(string raw, bool defaultValue)
        {
            bool parsed;
            return bool.TryParse(raw, out parsed) ? parsed : defaultValue;
        }

        private static string SafeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
        }
    }
}
