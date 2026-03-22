using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SHDocVw;

namespace WpfApplication1.Automation.IE
{
    public class IeBrowserService : IIeBrowserService
    {
        public async Task<IIePage> LaunchAsync(string url, int timeoutMs)
        {
            Exception launchException = null;
            IWebBrowser2 browser = null;

            try
            {
                browser = CreateBrowserViaCom();
            }
            catch (Exception ex)
            {
                launchException = ex;
            }

            if (browser != null)
            {
                try
                {
                    browser.Visible = true;

                    var page = new IePage(browser);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        await page.NavigateAsync(url, timeoutMs);
                    }
                    else
                    {
                        await page.WaitForReadyAsync(timeoutMs);
                    }

                    return page;
                }
                catch (Exception ex)
                {
                    TryQuit(browser);
                    throw CreateLaunchException(ex);
                }
            }

            var attachedPage = await TryLaunchByProcessAndAttachAsync(url, timeoutMs);
            if (attachedPage != null)
            {
                return attachedPage;
            }

            throw CreateLaunchException(launchException);
        }

        public async Task<IIePage> AttachAsync(int timeoutMs)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                var pages = GetAllPages();
                if (pages.Count > 0)
                {
                    return pages[pages.Count - 1];
                }

                await Task.Delay(200);
            }

            throw new InvalidOperationException("未找到可附加的 IE11 窗口。请先确认本机仍然能正常打开独立的 IE11 桌面浏览器。");
        }

        public IList<IIePage> GetAllPages()
        {
            try
            {
                var pages = new List<IIePage>();
                foreach (var browser in EnumerateIeBrowsers())
                {
                    pages.Add(new IePage(browser));
                }

                return pages;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "无法枚举当前系统中的 IE 窗口。请确认 IE11 桌面浏览器未被系统策略禁用，并且当前进程有权限访问 COM 自动化组件。"
                    + BuildRawErrorSuffix(ex),
                    ex);
            }
        }

        private static IWebBrowser2 CreateBrowserViaCom()
        {
            IWebBrowser2 browser = null;
            Exception activationException = null;

            activationException = TryCreateWithProgId(out browser);
            if (browser != null)
            {
                return browser;
            }

            activationException = TryCreateWithFactory(
                () => new InternetExplorerClass(),
                ref browser,
                activationException);
            if (browser != null)
            {
                return browser;
            }

            activationException = TryCreateWithFactory(
                () => new InternetExplorerMediumClass(),
                ref browser,
                activationException);
            if (browser != null)
            {
                return browser;
            }

            throw CreateLaunchException(activationException);
        }

        private static Exception TryCreateWithProgId(out IWebBrowser2 browser)
        {
            browser = null;

            try
            {
                var browserType = Type.GetTypeFromProgID("InternetExplorer.Application", false);
                if (browserType == null)
                {
                    return new COMException("系统中未注册 InternetExplorer.Application。", unchecked((int)0x80040154));
                }

                browser = Activator.CreateInstance(browserType) as IWebBrowser2;
                if (browser == null)
                {
                    return new InvalidCastException("已创建 IE COM 对象，但无法转换为 IWebBrowser2 接口。");
                }

                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static Exception TryCreateWithFactory(
            Func<IWebBrowser2> factory,
            ref IWebBrowser2 browser,
            Exception previousException)
        {
            try
            {
                browser = factory();
                return previousException;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private async Task<IIePage> TryLaunchByProcessAndAttachAsync(string url, int timeoutMs)
        {
            var iePath = ResolveIeExecutablePath();
            if (string.IsNullOrWhiteSpace(iePath) || !File.Exists(iePath))
            {
                return null;
            }

            var existingHandles = new HashSet<int>();
            try
            {
                foreach (var browser in EnumerateIeBrowsers())
                {
                    existingHandles.Add(browser.HWND);
                }
            }
            catch
            {
                return null;
            }

            Process process = null;
            try
            {
                var targetUrl = string.IsNullOrWhiteSpace(url) ? "about:blank" : url;
                process = Process.Start(new ProcessStartInfo
                {
                    FileName = iePath,
                    Arguments = targetUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                return null;
            }

            var waitTimeout = Math.Max(timeoutMs, 5000);
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < waitTimeout)
            {
                List<IWebBrowser2> browsers;
                try
                {
                    browsers = new List<IWebBrowser2>(EnumerateIeBrowsers());
                }
                catch
                {
                    return null;
                }

                foreach (var browser in browsers)
                {
                    if (existingHandles.Contains(browser.HWND))
                    {
                        continue;
                    }

                    var page = new IePage(browser);
                    await page.WaitForReadyAsync(timeoutMs);
                    return page;
                }

                if (process != null && process.HasExited)
                {
                    break;
                }

                await Task.Delay(200);
            }

            return null;
        }

        private static IEnumerable<IWebBrowser2> EnumerateIeBrowsers()
        {
            var shellWindows = new ShellWindowsClass();
            foreach (object candidate in shellWindows)
            {
                var browser = candidate as IWebBrowser2;
                if (IsInternetExplorerWindow(browser))
                {
                    yield return browser;
                }
            }
        }

        private static bool IsInternetExplorerWindow(IWebBrowser2 browser)
        {
            if (browser == null)
            {
                return false;
            }

            try
            {
                var fullName = browser.FullName;
                return !string.IsNullOrWhiteSpace(fullName)
                       && string.Equals(Path.GetFileName(fullName), "iexplore.exe", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveIeExecutablePath()
        {
            var x86Path = @"C:\Program Files (x86)\Internet Explorer\iexplore.exe";
            if (File.Exists(x86Path))
            {
                return x86Path;
            }

            var x64Path = @"C:\Program Files\Internet Explorer\iexplore.exe";
            if (File.Exists(x64Path))
            {
                return x64Path;
            }

            return null;
        }

        private static void TryQuit(IWebBrowser2 browser)
        {
            if (browser == null)
            {
                return;
            }

            try
            {
                browser.Quit();
            }
            catch
            {
            }
        }

        private static InvalidOperationException CreateLaunchException(Exception ex)
        {
            var message = "启动 IE 失败。";
            if (ex == null)
            {
                message += " 当前系统没有返回明确的异常，但未能创建可自动化的 IE11 浏览器实例。";
            }
            else if (MatchesHResult(ex, unchecked((int)0x800706B5)) || ContainsText(ex, "接口未知"))
            {
                message += " 系统返回“接口未知”，通常表示当前机器上的 IE COM 接口不可用，或者已被系统策略、浏览器重定向等机制禁用。";
            }
            else if (MatchesHResult(ex, unchecked((int)0x80080005)))
            {
                message += " 系统尝试启动 IE 进程失败，通常是 IE11 桌面浏览器已被禁用、首次启动被拦截，或被 Edge 接管。";
            }
            else if (MatchesHResult(ex, unchecked((int)0x80040154)))
            {
                message += " 当前系统未注册 Internet Explorer 的 COM 组件。";
            }
            else if (MatchesHResult(ex, unchecked((int)0x80070005)))
            {
                message += " 当前进程没有权限访问 IE 自动化组件。";
            }
            else
            {
                message += " 系统无法创建可自动化的 IE11 浏览器实例。";
            }

            message += " 请先在系统中手动确认：双击 iexplore.exe 是否仍能打开独立的 IE11 窗口，而不是跳转到 Edge。";
            message += BuildRawErrorSuffix(ex);
            return new InvalidOperationException(message, ex);
        }

        private static bool MatchesHResult(Exception ex, int hresult)
        {
            var current = ex;
            while (current != null)
            {
                if (current.HResult == hresult)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        private static bool ContainsText(Exception ex, string text)
        {
            var current = ex;
            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Message)
                    && current.Message.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        private static string BuildRawErrorSuffix(Exception ex)
        {
            if (ex == null)
            {
                return string.Empty;
            }

            var root = ex;
            while (root.InnerException != null)
            {
                root = root.InnerException;
            }

            return string.Format(" 原始错误：{0} (HRESULT: 0x{1:X8})", root.Message, root.HResult);
        }
    }
}
