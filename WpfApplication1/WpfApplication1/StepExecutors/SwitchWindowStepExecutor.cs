using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
            var startedAt = DateTime.UtcNow;
            IList<IIePage> lastPages = new List<IIePage>();

            while ((DateTime.UtcNow - startedAt).TotalMilliseconds < step.TimeoutMs)
            {
                var pages = _browserService.GetAllPages();
                lastPages = pages;
                var matched = FilterPages(pages, currentPage, excludeCurrent, titleContains, urlContains);
                var target = PickTargetWindow(matched, rawMode, rawIndex);
                if (target != null)
                {
                    target.Activate();
                    await target.WaitForReadyAsync(Math.Min(step.TimeoutMs, 5000));
                    context.CurrentPage = target;
                    context.CurrentBrowser = target;
                    return StepExecutionResult.Success("已切换到窗口：" + DescribePage(target));
                }

                if (!waitForNewWindow)
                {
                    break;
                }

                await Task.Delay(200);
            }

            return StepExecutionResult.Failure(
                "未找到匹配的 IE 窗口。标题包含=" + SafeValue(titleContains)
                + "，地址包含=" + SafeValue(urlContains)
                + "。候选窗口：" + DescribeCandidates(lastPages, currentPage));
        }

        private static List<IIePage> FilterPages(
            IList<IIePage> pages,
            IIePage currentPage,
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

                if (excludeCurrent && IsSamePage(page, currentPage))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(titleContains)
                    && !ContainsIgnoreCase(page.Title, titleContains))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(urlContains)
                    && !MatchesUrlHint(page, urlContains))
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

        private static bool MatchesUrlHint(IIePage page, string hint)
        {
            if (page == null)
            {
                return false;
            }

            if (ContainsIgnoreCase(page.Url, hint) || ContainsIgnoreCase(page.Title, hint))
            {
                return true;
            }

            var url = page.Url ?? string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            return BuildUrlVariants(url).Any(variant => ContainsIgnoreCase(variant, hint));
        }

        private static IEnumerable<string> BuildUrlVariants(string url)
        {
            yield return url ?? string.Empty;

            if (string.IsNullOrWhiteSpace(url))
            {
                yield break;
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(url.Replace("+", "%20"));
            }
            catch
            {
                decoded = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(decoded) && !string.Equals(decoded, url, StringComparison.OrdinalIgnoreCase))
            {
                yield return decoded;
            }

            var webDecoded = WebUtility.UrlDecode(url);
            if (!string.IsNullOrWhiteSpace(webDecoded)
                && !string.Equals(webDecoded, url, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(webDecoded, decoded, StringComparison.OrdinalIgnoreCase))
            {
                yield return webDecoded;
            }
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

        private static string DescribeCandidates(IList<IIePage> pages, IIePage currentPage)
        {
            if (pages == null || pages.Count == 0)
            {
                return "(none)";
            }

            var candidates = pages
                .Where(page => page != null)
                .Where(page => !IsSamePage(page, currentPage))
                .Take(5)
                .Select(DescribePage)
                .ToArray();
            return candidates.Length == 0 ? "(none)" : string.Join(" | ", candidates);
        }

        private static bool ContainsIgnoreCase(string source, string target)
        {
            return (source ?? string.Empty).IndexOf(target ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ParseBoolean(string raw, bool defaultValue)
        {
            bool parsed;
            return bool.TryParse(raw, out parsed) ? parsed : defaultValue;
        }

        private static string DescribePage(IIePage page)
        {
            return string.Format("Handle={0}, Title={1}, Url={2}",
                page != null ? page.WindowHandle : 0,
                page != null ? SafeValue(page.Title) : "(null)",
                page != null ? SafeValue(page.Url) : "(null)");
        }

        private static string SafeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
        }
    }
}
