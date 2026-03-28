using System;
using System.Collections.Generic;
using System.Threading;
using WpfApplication1.Models;
using WpfApplication1.Services;

namespace WpfApplication1.Workflow
{
    public interface IExecutionContext
    {
        IDictionary<string, object> Variables { get; }

        IDictionary<string, LoopPairInfo> LoopPairs { get; }

        IDictionary<string, LoopRuntimeState> LoopStates { get; }

        object CurrentBrowser { get; set; }

        object CurrentPage { get; set; }

        IBusinessStateStore BusinessStateStore { get; set; }

        string BusinessStatePath { get; set; }

        BusinessStateRecord CurrentBusinessState { get; set; }

        RuntimeStateSnapshot RuntimeState { get; }

        string SchedulerMode { get; set; }

        string CurrentWorkflowPath { get; set; }

        string WorkflowId { get; set; }

        string WorkflowName { get; set; }

        Enums.WorkflowType WorkflowType { get; set; }

        string RunId { get; set; }

        string RunName { get; set; }

        event Action<RuntimeStateSnapshot> RuntimeStateChanged;

        void UpdateRuntimeState(Action<RuntimeStateSnapshot> update);

        CancellationToken CancellationToken { get; }
    }
}
