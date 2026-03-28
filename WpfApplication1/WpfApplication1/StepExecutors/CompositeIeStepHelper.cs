using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Models;
using WpfApplication1.Selectors;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    internal static class CompositeIeStepHelper
    {
        public static IIePage ResolvePage(IExecutionContext context, string errorMessage)
        {
            var page = context.CurrentPage as IIePage;
            if (page == null)
            {
                throw new InvalidOperationException(errorMessage);
            }

            return page;
        }

        public static string NormalizeSelector(string selectorText)
        {
            if (string.IsNullOrWhiteSpace(selectorText))
            {
                return string.Empty;
            }

            var trimmed = selectorText.Trim();
            if (trimmed.IndexOf('=') >= 0)
            {
                return trimmed;
            }

            return "xpath=" + trimmed;
        }

        public static bool ElementExists(IIePage page, string selectorText)
        {
            if (page == null || string.IsNullOrWhiteSpace(selectorText))
            {
                return false;
            }

            try
            {
                page.FindElement(SelectorParser.Parse(NormalizeSelector(selectorText)));
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public static void ClickSelector(IIePage page, string selectorText)
        {
            page.FindElement(SelectorParser.Parse(NormalizeSelector(selectorText))).Click();
        }

        public static void SetValue(IIePage page, string selectorText, string value)
        {
            page.FindElement(SelectorParser.Parse(NormalizeSelector(selectorText))).SetValue(value ?? string.Empty);
        }

        public static void SelectOption(IIePage page, string selectorText, string option, bool byText)
        {
            page.FindElement(SelectorParser.Parse(NormalizeSelector(selectorText))).SelectOption(option ?? string.Empty, byText);
        }

        public static async Task WaitForElementAsync(IIePage page, string selectorText, int timeoutMs, int pollIntervalMs, CancellationToken cancellationToken)
        {
            var startedAt = DateTime.UtcNow;
            while ((DateTime.UtcNow - startedAt).TotalMilliseconds < timeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ElementExists(page, selectorText))
                {
                    return;
                }

                await Task.Delay(pollIntervalMs, cancellationToken);
            }

            throw new TimeoutException("等待元素超时：" + selectorText);
        }

        public static async Task<IIePage> WaitForWindowAsync(
            IIeBrowserService browserService,
            int currentHandle,
            string targetTitle,
            string matchMode,
            bool excludeCurrent,
            int timeoutMs,
            int pollIntervalMs,
            CancellationToken cancellationToken)
        {
            var startedAt = DateTime.UtcNow;
            while ((DateTime.UtcNow - startedAt).TotalMilliseconds < timeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var targetPage = browserService
                    .GetAllPages()
                    .Where(page => page != null)
                    .Where(page => !excludeCurrent || page.WindowHandle != currentHandle)
                    .FirstOrDefault(page => IsWindowTitleMatch(page.Title, targetTitle, matchMode));
                if (targetPage != null)
                {
                    targetPage.Activate();
                    await targetPage.WaitForReadyAsync(Math.Min(timeoutMs, 5000));
                    return targetPage;
                }

                await Task.Delay(pollIntervalMs, cancellationToken);
            }

            throw new TimeoutException("在超时时间内未找到目标窗口：" + targetTitle);
        }

        public static bool IsWindowTitleMatch(string title, string expectedTitle, string matchMode)
        {
            var actual = title ?? string.Empty;
            var expected = expectedTitle ?? string.Empty;
            switch (NormalizeMatchMode(matchMode))
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

        public static string NormalizeMatchMode(string rawMatchMode)
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

        public static int ResolvePositiveInt(string raw, int defaultValue)
        {
            int value;
            return int.TryParse(raw, out value) && value > 0 ? value : defaultValue;
        }

        public static bool ResolveBoolean(string raw, bool defaultValue)
        {
            bool value;
            return bool.TryParse(raw, out value) ? value : defaultValue;
        }

        public static string ResolveTemplate(string template, IVariableResolver variableResolver, IExecutionContext context, int rowIndex)
        {
            var resolved = variableResolver != null ? variableResolver.ResolveString(template, context) : template;
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return string.Empty;
            }

            return resolved.Replace("${RowIndex}", rowIndex.ToString());
        }

        public static string EnsureDirectory(string directory)
        {
            var finalDirectory = string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports")
                : Path.GetFullPath(directory);
            Directory.CreateDirectory(finalDirectory);
            return finalDirectory;
        }

        public static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "report";
            }

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(invalidChar, '_');
            }

            return raw;
        }

        public static void TryCloseWindow(IIePage page)
        {
            if (page == null)
            {
                return;
            }

            try
            {
                page.ExecuteScript("window.close();");
            }
            catch
            {
            }
        }
    }
}