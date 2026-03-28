using System;
using System.Collections.Generic;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Workflow;

namespace WpfApplication1.Services
{
    public interface ILogService
    {
        event EventHandler<ExecutionLogEntry> EntryAdded;

        WorkflowLogRunItem CurrentRun { get; }

        IReadOnlyList<ExecutionLogEntry> CurrentEntries { get; }

        void Clear();

        void BeginRun(WorkflowDefinition workflow, string workflowPath, string runMode, string runName = null);

        void EndRun(string result, string summary = null);

        void Log(LogLevel level, string message, WorkflowStep step = null, string screenshotPath = null, IExecutionContext context = null, string status = null, int attempt = 0, int maxAttempts = 0, Exception exception = null);
    }
}
