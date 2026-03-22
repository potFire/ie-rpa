using System.Collections.Generic;
using System.Threading;

namespace WpfApplication1.Workflow
{
    public interface IExecutionContext
    {
        IDictionary<string, object> Variables { get; }

        object CurrentBrowser { get; set; }

        object CurrentPage { get; set; }

        CancellationToken CancellationToken { get; }
    }
}
