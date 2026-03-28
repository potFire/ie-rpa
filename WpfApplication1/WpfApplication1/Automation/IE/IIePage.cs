using System.Collections.Generic;
using WpfApplication1.Models;

namespace WpfApplication1.Automation.IE
{
    public interface IIePage
    {
        string Title { get; }

        string Url { get; }

        int WindowHandle { get; }

        string BrowserIdentityKey { get; }

        string FramePathDisplay { get; }

        int FrameDepth { get; }

        System.Threading.Tasks.Task NavigateAsync(string url, int timeoutMs);

        System.Threading.Tasks.Task WaitForReadyAsync(int timeoutMs);

        IIeElement FindElement(SelectorDefinition selector);

        IIePage EnterFrame(SelectorDefinition selector);

        IIePage GetParentFramePage();

        IIePage GetRootPage();

        IIePage GetFramePage(string framePath);

        void ExecuteScript(string script);

        string EvaluateScript(string expression);

        string GetHtml();

        void Activate();

        IList<ElementSummary> ListElements(int maxCount);

        void HighlightElement(SelectorDefinition selector);
    }
}
