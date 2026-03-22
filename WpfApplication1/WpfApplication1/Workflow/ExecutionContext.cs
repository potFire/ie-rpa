using System.Collections.Generic;
using System.Threading;

namespace WpfApplication1.Workflow
{
    public class ExecutionContext : IExecutionContext
    {
        public ExecutionContext(CancellationToken cancellationToken)
        {
            Variables = new Dictionary<string, object>();
            CancellationToken = cancellationToken;
        }

        public IDictionary<string, object> Variables { get; private set; }

        public object CurrentBrowser { get; set; }

        public object CurrentPage { get; set; }

        public CancellationToken CancellationToken { get; private set; }
    }
}
