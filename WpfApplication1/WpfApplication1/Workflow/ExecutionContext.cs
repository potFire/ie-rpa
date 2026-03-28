using System;
using System.Collections.Generic;
using System.Threading;
using WpfApplication1.Automation.IE;
using WpfApplication1.Models;
using WpfApplication1.Services;

namespace WpfApplication1.Workflow
{
    public class ExecutionContext : IExecutionContext
    {
        public ExecutionContext(CancellationToken cancellationToken)
        {
            Variables = new Dictionary<string, object>();
            LoopPairs = new Dictionary<string, LoopPairInfo>();
            LoopStates = new Dictionary<string, LoopRuntimeState>();
            CancellationToken = cancellationToken;
            RuntimeState = new RuntimeStateSnapshot();
        }

        public IDictionary<string, object> Variables { get; private set; }

        public IDictionary<string, LoopPairInfo> LoopPairs { get; private set; }

        public IDictionary<string, LoopRuntimeState> LoopStates { get; private set; }

        public object CurrentBrowser { get; set; }

        public object CurrentPage
        {
            get { return _currentPage; }
            set
            {
                _currentPage = value;
                SyncPageRuntimeInfo();
            }
        }

        public IBusinessStateStore BusinessStateStore { get; set; }

        public string BusinessStatePath { get; set; }

        public BusinessStateRecord CurrentBusinessState { get; set; }

        public RuntimeStateSnapshot RuntimeState { get; private set; }

        public string SchedulerMode
        {
            get { return _schedulerMode; }
            set
            {
                _schedulerMode = value;
                UpdateRuntimeState(state => state.CurrentMode = value ?? string.Empty);
            }
        }

        public string CurrentWorkflowPath
        {
            get { return _currentWorkflowPath; }
            set
            {
                _currentWorkflowPath = value;
                UpdateRuntimeState(state => state.CurrentWorkflowPath = value ?? string.Empty);
            }
        }

        public string WorkflowId { get; set; }

        public string WorkflowName { get; set; }

        public Enums.WorkflowType WorkflowType { get; set; }

        public string RunId { get; set; }

        public string RunName { get; set; }

        public event Action<RuntimeStateSnapshot> RuntimeStateChanged;

        public CancellationToken CancellationToken { get; private set; }

        private object _currentPage;
        private string _schedulerMode;
        private string _currentWorkflowPath;

        public void UpdateRuntimeState(Action<RuntimeStateSnapshot> update)
        {
            if (update == null)
            {
                return;
            }

            update(RuntimeState);
            RuntimeState.LastUpdatedAt = DateTime.Now;
            var handler = RuntimeStateChanged;
            if (handler != null)
            {
                handler(RuntimeState.Clone());
            }
        }

        private void SyncPageRuntimeInfo()
        {
            var page = _currentPage as IIePage;
            UpdateRuntimeState(state =>
            {
                state.CurrentWindowTitle = page != null ? (page.Title ?? string.Empty) : string.Empty;
                state.CurrentPageUrl = page != null ? (page.Url ?? string.Empty) : string.Empty;
                state.FramePathDisplay = page != null ? (page.FramePathDisplay ?? "root") : "root";
                state.FrameDepth = page != null ? page.FrameDepth : 0;
            });
        }
    }
}
