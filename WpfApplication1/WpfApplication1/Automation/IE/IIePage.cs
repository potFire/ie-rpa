using System.Collections.Generic;
using WpfApplication1.Models;

namespace WpfApplication1.Automation.IE
{
    public interface IIePage
    {
        string Title { get; }

        string Url { get; }

        int WindowHandle { get; }

        System.Threading.Tasks.Task NavigateAsync(string url, int timeoutMs);

        System.Threading.Tasks.Task WaitForReadyAsync(int timeoutMs);

        IIeElement FindElement(SelectorDefinition selector);

        IIePage GetFramePage(string framePath);

        void ExecuteScript(string script);

        string EvaluateScript(string expression);

        string GetHtml();

        // 元素拾取器会用到这个接口，把当前页面里较常用的元素信息列出来。
        // 第一版不做复杂录制，但至少要支持高亮预览，方便用户确认 selector 是否准确。
        IList<ElementSummary> ListElements(int maxCount);

        void HighlightElement(SelectorDefinition selector);
    }
}
