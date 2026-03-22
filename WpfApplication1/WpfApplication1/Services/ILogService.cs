using System;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public interface ILogService
    {
        event EventHandler<ExecutionLogEntry> EntryAdded;

        void Clear();

        void Log(LogLevel level, string message, WorkflowStep step = null, string screenshotPath = null);
    }
}
