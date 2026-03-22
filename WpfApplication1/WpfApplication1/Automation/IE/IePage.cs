using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using mshtml;
using SHDocVw;
using WpfApplication1.Models;

namespace WpfApplication1.Automation.IE
{
    public class IePage : IIePage
    {
        private const string HighlightMarkerAttribute = "data-ie-rpa-highlight";
        private const string PreviousBorderAttribute = "data-ie-rpa-prev-border";
        private const string PreviousBackgroundAttribute = "data-ie-rpa-prev-background";
        private readonly IWebBrowser2 _browser;
        private readonly IHTMLDocument2 _document;

        public IePage(IWebBrowser2 browser)
            : this(browser, null)
        {
        }

        public IePage(IWebBrowser2 browser, IHTMLDocument2 document)
        {
            if (browser == null)
            {
                throw new ArgumentNullException("browser");
            }

            _browser = browser;
            _document = document;
        }

        public string Title
        {
            get
            {
                var document = GetCurrentDocument();
                return document != null ? document.title : _browser.LocationName;
            }
        }

        public string Url
        {
            get
            {
                var document = GetCurrentDocument();
                return document != null ? document.url : _browser.LocationURL;
            }
        }

        public int WindowHandle
        {
            get { return _browser.HWND; }
        }

        public async Task NavigateAsync(string url, int timeoutMs)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException("导航地址不能为空。");
            }

            _browser.Navigate(url);
            await WaitForReadyAsync(timeoutMs);
        }

        public async Task WaitForReadyAsync(int timeoutMs)
        {
            var startedAt = DateTime.UtcNow;
            while ((DateTime.UtcNow - startedAt).TotalMilliseconds < timeoutMs)
            {
                if (!Convert.ToBoolean(_browser.Busy)
                    && _browser.ReadyState == tagREADYSTATE.READYSTATE_COMPLETE)
                {
                    var document = GetCurrentDocument();
                    if (document != null)
                    {
                        var readyState = document.readyState;
                        if (string.Equals(readyState, "complete", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(readyState, "interactive", StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                    }
                }

                await Task.Delay(150);
            }

            throw new TimeoutException("等待 IE 页面加载完成超时。");
        }

        public IIeElement FindElement(SelectorDefinition selector)
        {
            var document = ResolveDocument(selector != null ? selector.FramePath : null);
            if (document == null)
            {
                throw new InvalidOperationException("当前页面文档不可用。");
            }

            var matchedElement = FindDomElement(document, selector);
            if (matchedElement == null)
            {
                throw new InvalidOperationException("未找到匹配的 IE 元素。");
            }

            return new IeElement(matchedElement);
        }

        public IIePage GetFramePage(string framePath)
        {
            if (string.IsNullOrWhiteSpace(framePath) || string.Equals(framePath, "root", StringComparison.OrdinalIgnoreCase))
            {
                return new IePage(_browser);
            }

            return new IePage(_browser, ResolveDocument(framePath));
        }

        public void ExecuteScript(string script)
        {
            var window = GetPageWindow();
            window.execScript(script, "javascript");
        }

        public string EvaluateScript(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return string.Empty;
            }

            var window = GetPageWindow();
            var result = window.GetType().InvokeMember(
                "eval",
                BindingFlags.InvokeMethod,
                null,
                window,
                new object[] { expression });

            return result != null ? Convert.ToString(result, CultureInfo.InvariantCulture) : string.Empty;
        }

        public string GetHtml()
        {
            var document = GetCurrentDocument() as IHTMLDocument3;
            if (document == null || document.documentElement == null)
            {
                return string.Empty;
            }

            return document.documentElement.outerHTML;
        }

        public IList<ElementSummary> ListElements(int maxCount)
        {
            var result = new List<ElementSummary>();
            var document = GetCurrentDocument();
            if (document == null)
            {
                return result;
            }

            var count = 0;
            foreach (IHTMLElement element in document.all)
            {
                if (element == null)
                {
                    continue;
                }

                var tagName = (element.tagName ?? string.Empty).ToLowerInvariant();
                if (tagName == "html" || tagName == "head" || tagName == "script" || tagName == "style")
                {
                    continue;
                }

                var summary = new ElementSummary
                {
                    TagName = tagName,
                    Id = Convert.ToString(element.getAttribute("id", 0)),
                    Name = Convert.ToString(element.getAttribute("name", 0)),
                    Text = NormalizePreviewText(element.innerText),
                    Value = NormalizePreviewText(Convert.ToString(element.getAttribute("value", 0))),
                    Selector = BuildSuggestedSelector(element)
                };
                result.Add(summary);

                count++;
                if (count >= maxCount)
                {
                    break;
                }
            }

            return result;
        }

        public void HighlightElement(SelectorDefinition selector)
        {
            var document = ResolveDocument(selector != null ? selector.FramePath : null);
            if (document == null)
            {
                throw new InvalidOperationException("当前页面文档不可用，无法高亮元素。");
            }

            ClearExistingHighlights(document);

            var matchedElement = FindDomElement(document, selector);
            if (matchedElement == null)
            {
                throw new InvalidOperationException("未找到可高亮的元素。");
            }

            var style = matchedElement.style;
            var currentBorder = style != null ? style.border : string.Empty;
            var currentBackground = style != null ? style.backgroundColor : string.Empty;
            matchedElement.setAttribute(PreviousBorderAttribute, currentBorder, 0);
            matchedElement.setAttribute(PreviousBackgroundAttribute, currentBackground, 0);
            matchedElement.setAttribute(HighlightMarkerAttribute, "1", 0);

            if (style != null)
            {
                style.border = "2px solid #ff4d4f";
                style.backgroundColor = "#fff2b8";
            }

            var element2 = matchedElement as IHTMLElement2;
            if (element2 != null)
            {
                matchedElement.GetType().InvokeMember(
                    "scrollIntoView",
                    BindingFlags.InvokeMethod,
                    null,
                    matchedElement,
                    new object[] { true });
                element2.focus();
            }
        }

        private static string BuildSuggestedSelector(IHTMLElement element)
        {
            var xpath = BuildXPath(element);
            if (!string.IsNullOrWhiteSpace(xpath))
            {
                return "xpath=" + xpath;
            }

            var id = Convert.ToString(element.getAttribute("id", 0));
            if (!string.IsNullOrWhiteSpace(id))
            {
                return "id=" + id;
            }

            var name = Convert.ToString(element.getAttribute("name", 0));
            if (!string.IsNullOrWhiteSpace(name))
            {
                return "name=" + name + ";tag=" + (element.tagName ?? string.Empty).ToLowerInvariant();
            }

            return "tag=" + (element.tagName ?? string.Empty).ToLowerInvariant();
        }

        private static string BuildXPath(IHTMLElement element)
        {
            if (element == null)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            var current = element;
            while (current != null)
            {
                var tagName = (current.tagName ?? string.Empty).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    break;
                }

                segments.Insert(0, "/" + tagName + "[" + GetSameTagIndex(current) + "]");
                current = current.parentElement;
            }

            return string.Join(string.Empty, segments.ToArray());
        }

        private static int GetSameTagIndex(IHTMLElement element)
        {
            var parent = element.parentElement;
            if (parent == null)
            {
                return 1;
            }

            var collection = parent.children as IHTMLElementCollection;
            if (collection == null)
            {
                return 1;
            }

            var index = 0;
            foreach (object childObject in collection)
            {
                var child = childObject as IHTMLElement;
                if (child == null)
                {
                    continue;
                }

                if (!StringEquals(child.tagName, element.tagName))
                {
                    continue;
                }

                index++;
                if (ReferenceEquals(child, element))
                {
                    return index;
                }
            }

            return 1;
        }

        private static string NormalizePreviewText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var normalized = Regex.Replace(raw, "\\s+", " ").Trim();
            if (normalized.Length > 60)
            {
                return normalized.Substring(0, 60);
            }

            return normalized;
        }

        private IHTMLWindow2 GetPageWindow()
        {
            var document = GetCurrentDocument();
            if (document == null)
            {
                throw new InvalidOperationException("当前页面文档不可用。");
            }

            var window = document.parentWindow;
            if (window == null)
            {
                throw new InvalidOperationException("当前页面窗口不可用。");
            }

            return window;
        }

        private IHTMLDocument2 GetCurrentDocument()
        {
            if (_document != null)
            {
                return _document;
            }

            try
            {
                return _browser.Document as IHTMLDocument2;
            }
            catch (COMException)
            {
                return null;
            }
        }

        private IHTMLDocument2 ResolveDocument(string framePath)
        {
            var document = GetCurrentDocument();
            if (document == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(framePath))
            {
                return document;
            }

            var parts = framePath.Split(new[] { '/', '>' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawPart in parts)
            {
                var frames = document.frames as IHTMLFramesCollection2;
                if (frames == null)
                {
                    throw new InvalidOperationException("当前页面不包含可切换的 frame。");
                }

                object frameKey;
                int frameIndex;
                if (int.TryParse(rawPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out frameIndex))
                {
                    frameKey = frameIndex;
                }
                else
                {
                    frameKey = rawPart;
                }

                var frameWindow = frames.item(ref frameKey) as IHTMLWindow2;
                if (frameWindow == null || frameWindow.document == null)
                {
                    throw new InvalidOperationException("未找到指定的 frame：" + rawPart);
                }

                document = frameWindow.document;
            }

            return document;
        }

        private static IHTMLElement FindDomElement(IHTMLDocument2 document, SelectorDefinition selector)
        {
            if (selector != null && !string.IsNullOrWhiteSpace(selector.XPath))
            {
                return FindElementByXPath(document, selector.XPath);
            }

            IHTMLElement matchedElement = null;
            var matchedCount = 0;
            foreach (IHTMLElement candidate in document.all)
            {
                if (!IsMatch(candidate, selector))
                {
                    continue;
                }

                if (selector != null && selector.Index.HasValue)
                {
                    if (matchedCount == selector.Index.Value)
                    {
                        matchedElement = candidate;
                        break;
                    }

                    matchedCount++;
                    continue;
                }

                matchedElement = candidate;
                break;
            }

            return matchedElement;
        }

        private static IHTMLElement FindElementByXPath(IHTMLDocument2 document, string xpath)
        {
            if (document == null || string.IsNullOrWhiteSpace(xpath))
            {
                return null;
            }

            var document3 = document as IHTMLDocument3;
            var current = document3 != null ? document3.documentElement as IHTMLElement : null;
            if (current == null)
            {
                return null;
            }

            var segments = xpath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            for (var i = 0; i < segments.Length; i++)
            {
                string tagName;
                int index;
                if (!TryParseXPathSegment(segments[i], out tagName, out index))
                {
                    return null;
                }

                if (i == 0)
                {
                    if (!(tagName == "*" || StringEquals(current.tagName, tagName)) || index != 1)
                    {
                        return null;
                    }

                    continue;
                }

                current = FindChildElement(current, tagName, index);
                if (current == null)
                {
                    return null;
                }
            }

            return current;
        }

        private static bool TryParseXPathSegment(string segment, out string tagName, out int index)
        {
            tagName = null;
            index = 1;
            if (string.IsNullOrWhiteSpace(segment))
            {
                return false;
            }

            var match = Regex.Match(segment.Trim(), "^(?<tag>[a-zA-Z0-9_\\*:-]+)(\\[(?<index>\\d+)\\])?$");
            if (!match.Success)
            {
                return false;
            }

            tagName = match.Groups["tag"].Value.ToLowerInvariant();
            var indexGroup = match.Groups["index"];
            if (indexGroup.Success)
            {
                index = Convert.ToInt32(indexGroup.Value, CultureInfo.InvariantCulture);
            }

            return index > 0;
        }

        private static IHTMLElement FindChildElement(IHTMLElement parent, string tagName, int index)
        {
            if (parent == null)
            {
                return null;
            }

            var collection = parent.children as IHTMLElementCollection;
            if (collection == null)
            {
                return null;
            }

            var currentIndex = 0;
            foreach (object childObject in collection)
            {
                var child = childObject as IHTMLElement;
                if (child == null)
                {
                    continue;
                }

                if (!(tagName == "*" || StringEquals(child.tagName, tagName)))
                {
                    continue;
                }

                currentIndex++;
                if (currentIndex == index)
                {
                    return child;
                }
            }

            return null;
        }

        private static void ClearExistingHighlights(IHTMLDocument2 document)
        {
            foreach (IHTMLElement element in document.all)
            {
                if (element == null)
                {
                    continue;
                }

                var marker = Convert.ToString(element.getAttribute(HighlightMarkerAttribute, 0));
                if (!string.Equals(marker, "1", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var style = element.style;
                if (style != null)
                {
                    style.border = Convert.ToString(element.getAttribute(PreviousBorderAttribute, 0));
                    style.backgroundColor = Convert.ToString(element.getAttribute(PreviousBackgroundAttribute, 0));
                }

                element.removeAttribute(HighlightMarkerAttribute, 0);
                element.removeAttribute(PreviousBorderAttribute, 0);
                element.removeAttribute(PreviousBackgroundAttribute, 0);
            }
        }

        private static bool IsMatch(IHTMLElement element, SelectorDefinition selector)
        {
            if (element == null)
            {
                return false;
            }

            if (selector == null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(selector.Id)
                && !StringEquals(Convert.ToString(element.getAttribute("id", 0)), selector.Id))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(selector.Name)
                && !StringEquals(Convert.ToString(element.getAttribute("name", 0)), selector.Name))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(selector.TagName)
                && !StringEquals(element.tagName, selector.TagName))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(selector.Text))
            {
                var innerText = element.innerText ?? string.Empty;
                if (innerText.IndexOf(selector.Text, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            if (selector.Attributes != null)
            {
                foreach (var pair in selector.Attributes)
                {
                    if (!StringEquals(Convert.ToString(element.getAttribute(pair.Key, 0)), pair.Value))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }
}