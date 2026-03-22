using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class SwitchWindowStepExecutor : IStepExecutor
    {
        private readonly IIeBrowserService _browserService;
        private readonly IVariableResolver _variableResolver;

        public SwitchWindowStepExecutor(IIeBrowserService browserService, IVariableResolver variableResolver)
        {
            _browserService = browserService;
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.SwitchWindow; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var currentPage = context.CurrentPage as IIePage;
            string titleContains;
            string urlContains;
            string rawIndex;
            string rawMode;
            string rawWaitForNewWindow;
            string rawExcludeCurrent;

            step.Parameters.TryGetValue("titleContains", out titleContains);
            step.Parameters.TryGetValue("urlContains", out urlContains);
            step.Parameters.TryGetValue("index", out rawIndex);
            step.Parameters.TryGetValue("mode", out rawMode);
            step.Parameters.TryGetValue("waitForNewWindow", out rawWaitForNewWindow);
            step.Parameters.TryGetValue("excludeCurrent", out rawExcludeCurrent);

            titleContains = _variableResolver.ResolveString(titleContains, context);
            urlContains = _variableResolver.ResolveString(urlContains, context);
            rawIndex = _variableResolver.ResolveString(rawIndex, context);
            rawMode = _variableResolver.ResolveString(rawMode, context);

            var waitForNewWindow = ParseBoolean(rawWaitForNewWindow, false);
            var excludeCurrent = ParseBoolean(rawExcludeCurrent, true);
            var currentHandle = currentPage != null ? currentPage.WindowHandle : 0;
            var startedAt = DateTime.UtcNow;

            while ((DateTime.UtcNow - startedAt).TotalMilliseconds < step.TimeoutMs)
            {
                var pages = _browserService.GetAllPages();
                var matched = FilterPages(pages, currentHandle, excludeCurrent, titleContains, urlContains);
                var target = PickTargetWindow(matched, rawMode, rawIndex);
                if (target != null)
                {
                    context.CurrentPage = target;
                    context.CurrentBrowser = target;
                    return StepExecutionResult.Success("已切换到窗口：" + SafeWindowLabel(target));
                }

                if (!waitForNewWindow)
                {
                    break;
                }

                await Task.Delay(200);
            }

            return StepExecutionResult.Failure("未找到匹配的 IE 窗口。");
        }

        private static List<IIePage> FilterPages(
            IList<IIePage> pages,
            int currentHandle,
            bool excludeCurrent,
            string titleContains,
            string urlContains)
        {
            var result = new List<IIePage>();
            foreach (var page in pages)
            {
                if (page == null)
                {
                    continue;
                }

                if (excludeCurrent && currentHandle != 0 && page.WindowHandle == currentHandle)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(titleContains)
                    && (page.Title ?? string.Empty).IndexOf(titleContains, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(urlContains)
                    && (page.Url ?? string.Empty).IndexOf(urlContains, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                result.Add(page);
            }

            return result;
        }

        private static IIePage PickTargetWindow(IList<IIePage> pages, string mode, string rawIndex)
        {
            if (pages == null || pages.Count == 0)
            {
                return null;
            }

            // 默认选择最后一个窗口，更贴近“点击后弹出新窗口”的业务习惯。
            if (string.IsNullOrWhiteSpace(mode) || string.Equals(mode, "last", StringComparison.OrdinalIgnoreCase))
            {
                return pages[pages.Count - 1];
            }

            if (string.Equals(mode, "first", StringComparison.OrdinalIgnoreCase))
            {
                return pages[0];
            }

            if (string.Equals(mode, "index", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (int.TryParse(rawIndex, out index) && index >= 0 && index < pages.Count)
                {
                    return pages[index];
                }
            }

            return pages[pages.Count - 1];
        }

        private static bool ParseBoolean(string raw, bool defaultValue)
        {
            bool parsed;
            return bool.TryParse(raw, out parsed) ? parsed : defaultValue;
        }

        private static string SafeWindowLabel(IIePage page)
        {
            if (page == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(page.Title))
            {
                return page.Title;
            }

            return page.Url ?? string.Empty;
        }
    }
}
