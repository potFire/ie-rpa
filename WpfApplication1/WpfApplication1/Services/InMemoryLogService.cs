using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Workflow;

namespace WpfApplication1.Services
{
    public class InMemoryLogService : ILogService
    {
        private readonly object _syncRoot = new object();
        private readonly string _baseDirectory;
        private readonly string _applicationDirectory;
        private readonly List<ExecutionLogEntry> _currentEntries;
        private string _fallbackLogFilePath;
        private WorkflowLogRunItem _currentRun;

        public InMemoryLogService()
        {
            _baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            _applicationDirectory = Path.Combine(_baseDirectory, "Application");
            _currentEntries = new List<ExecutionLogEntry>();
            Directory.CreateDirectory(_baseDirectory);
            ResetFallbackFile();
        }

        public event EventHandler<ExecutionLogEntry> EntryAdded;

        public WorkflowLogRunItem CurrentRun
        {
            get { return _currentRun; }
        }

        public IReadOnlyList<ExecutionLogEntry> CurrentEntries
        {
            get { return _currentEntries.AsReadOnly(); }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _currentEntries.Clear();
                _currentRun = null;
                ResetFallbackFile();
            }
        }

        public void BeginRun(WorkflowDefinition workflow, string workflowPath, string runMode, string runName = null)
        {
            if (workflow == null)
            {
                throw new ArgumentNullException("workflow");
            }

            lock (_syncRoot)
            {
                _currentEntries.Clear();
                var workflowId = string.IsNullOrWhiteSpace(workflow.Id) ? Guid.NewGuid().ToString("N") : workflow.Id;
                var workflowName = string.IsNullOrWhiteSpace(workflow.Name) ? workflowId : workflow.Name;
                var startedAt = DateTime.Now;
                var dayDirectory = Path.Combine(_baseDirectory, SanitizeSegment(workflowId), startedAt.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(dayDirectory);
                var runId = startedAt.ToString("yyyyMMddHHmmssfff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                _currentRun = new WorkflowLogRunItem
                {
                    WorkflowId = workflowId,
                    WorkflowName = workflowName,
                    WorkflowType = workflow.WorkflowType,
                    WorkflowPath = workflowPath ?? string.Empty,
                    RunId = runId,
                    RunName = string.IsNullOrWhiteSpace(runName) ? (string.IsNullOrWhiteSpace(runMode) ? "流程执行" : runMode) : runName,
                    RunMode = runMode ?? string.Empty,
                    Result = "Running",
                    Summary = string.Empty,
                    StartedAt = startedAt,
                    LogFilePath = Path.Combine(dayDirectory, runId + ".log")
                };

                WriteCurrentRunFile();
            }
        }

        public void EndRun(string result, string summary = null)
        {
            lock (_syncRoot)
            {
                if (_currentRun == null)
                {
                    return;
                }

                _currentRun.Result = string.IsNullOrWhiteSpace(result) ? "Completed" : result;
                _currentRun.Summary = summary ?? string.Empty;
                _currentRun.EndedAt = DateTime.Now;
                WriteCurrentRunFile();
            }
        }

        public void Log(LogLevel level, string message, WorkflowStep step = null, string screenshotPath = null, IExecutionContext context = null, string status = null, int attempt = 0, int maxAttempts = 0, Exception exception = null)
        {
            var runtimeState = context != null ? context.RuntimeState : null;
            var entry = new ExecutionLogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                WorkflowId = _currentRun != null ? _currentRun.WorkflowId : (context != null ? context.WorkflowId : string.Empty),
                WorkflowName = _currentRun != null ? _currentRun.WorkflowName : (context != null ? context.WorkflowName : string.Empty),
                WorkflowType = _currentRun != null ? (WorkflowType?)_currentRun.WorkflowType : (context != null ? (WorkflowType?)context.WorkflowType : null),
                RunId = _currentRun != null ? _currentRun.RunId : (context != null ? context.RunId : string.Empty),
                RunName = _currentRun != null ? _currentRun.RunName : (context != null ? context.RunName : string.Empty),
                RunMode = _currentRun != null ? _currentRun.RunMode : (context != null ? context.SchedulerMode : string.Empty),
                StepId = step != null ? step.Id : string.Empty,
                StepName = step != null ? step.Name : string.Empty,
                StepType = step != null ? (StepType?)step.StepType : null,
                Attempt = attempt,
                MaxAttempts = maxAttempts,
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                ScreenshotPath = screenshotPath,
                CurrentPageUrl = runtimeState != null ? runtimeState.CurrentPageUrl : string.Empty,
                CurrentWindowTitle = runtimeState != null ? runtimeState.CurrentWindowTitle : string.Empty,
                FramePathDisplay = runtimeState != null ? runtimeState.FramePathDisplay : string.Empty,
                CurrentObject = runtimeState != null ? runtimeState.CurrentObject : string.Empty,
                ExceptionType = exception != null ? exception.GetType().FullName : string.Empty
            };
            entry.FormattedLine = FormatEntryLine(entry);

            lock (_syncRoot)
            {
                _currentEntries.Add(entry);
                AppendEntryToFile(entry);
            }

            var handler = EntryAdded;
            if (handler != null)
            {
                handler(this, entry);
            }
        }

        private void ResetFallbackFile()
        {
            var directory = Path.Combine(_applicationDirectory, DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(directory);
            _fallbackLogFilePath = Path.Combine(directory, "session_" + DateTime.Now.ToString("HHmmssfff") + ".log");
            File.WriteAllText(_fallbackLogFilePath, "# IE RPA Application Log" + Environment.NewLine + "# -- entries --" + Environment.NewLine, Encoding.UTF8);
        }

        private void WriteCurrentRunFile()
        {
            if (_currentRun == null || string.IsNullOrWhiteSpace(_currentRun.LogFilePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_currentRun.LogFilePath));
            var builder = new StringBuilder();
            builder.AppendLine("# IE RPA Workflow Log");
            builder.AppendLine("# WorkflowId: " + (_currentRun.WorkflowId ?? string.Empty));
            builder.AppendLine("# WorkflowName: " + (_currentRun.WorkflowName ?? string.Empty));
            builder.AppendLine("# WorkflowType: " + _currentRun.WorkflowType);
            builder.AppendLine("# WorkflowPath: " + (_currentRun.WorkflowPath ?? string.Empty));
            builder.AppendLine("# RunId: " + (_currentRun.RunId ?? string.Empty));
            builder.AppendLine("# RunName: " + (_currentRun.RunName ?? string.Empty));
            builder.AppendLine("# RunMode: " + (_currentRun.RunMode ?? string.Empty));
            builder.AppendLine("# Result: " + (_currentRun.Result ?? string.Empty));
            builder.AppendLine("# StartedAt: " + _currentRun.StartedAt.ToString("o"));
            builder.AppendLine("# EndedAt: " + (_currentRun.EndedAt.HasValue ? _currentRun.EndedAt.Value.ToString("o") : string.Empty));
            builder.AppendLine("# Summary: " + (_currentRun.Summary ?? string.Empty));
            builder.AppendLine("# -- entries --");
            foreach (var entry in _currentEntries)
            {
                builder.AppendLine(entry.FormattedLine ?? FormatEntryLine(entry));
            }

            File.WriteAllText(_currentRun.LogFilePath, builder.ToString(), new UTF8Encoding(false));
        }

        private void AppendEntryToFile(ExecutionLogEntry entry)
        {
            var targetPath = _currentRun != null && !string.IsNullOrWhiteSpace(_currentRun.LogFilePath)
                ? _currentRun.LogFilePath
                : _fallbackLogFilePath;
            File.AppendAllText(targetPath, (entry.FormattedLine ?? FormatEntryLine(entry)) + Environment.NewLine, new UTF8Encoding(false));
        }

        private static string FormatEntryLine(ExecutionLogEntry entry)
        {
            var builder = new StringBuilder();
            builder.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}]", entry.Timestamp, entry.Level);
            if (!string.IsNullOrWhiteSpace(entry.Status))
            {
                builder.Append(" [Status:").Append(entry.Status).Append(']');
            }

            if (!string.IsNullOrWhiteSpace(entry.RunMode))
            {
                builder.Append(" [Mode:").Append(entry.RunMode).Append(']');
            }

            if (!string.IsNullOrWhiteSpace(entry.StepName) || entry.StepType.HasValue)
            {
                builder.Append(" [Step:")
                    .Append(string.IsNullOrWhiteSpace(entry.StepName) ? "-" : entry.StepName)
                    .Append('/')
                    .Append(entry.StepType.HasValue ? entry.StepType.Value.ToString() : "-")
                    .Append(']');
            }

            if (entry.Attempt > 0 && entry.MaxAttempts > 0)
            {
                builder.Append(" [Attempt:").Append(entry.Attempt).Append('/').Append(entry.MaxAttempts).Append(']');
            }

            builder.Append(' ').Append(NormalizeText(entry.Message));

            if (!string.IsNullOrWhiteSpace(entry.CurrentObject))
            {
                builder.Append(" | Object=").Append(NormalizeText(entry.CurrentObject));
            }

            if (!string.IsNullOrWhiteSpace(entry.CurrentPageUrl))
            {
                builder.Append(" | Url=").Append(NormalizeText(entry.CurrentPageUrl));
            }

            if (!string.IsNullOrWhiteSpace(entry.CurrentWindowTitle))
            {
                builder.Append(" | Window=").Append(NormalizeText(entry.CurrentWindowTitle));
            }

            if (!string.IsNullOrWhiteSpace(entry.FramePathDisplay))
            {
                builder.Append(" | Frame=").Append(NormalizeText(entry.FramePathDisplay));
            }

            if (!string.IsNullOrWhiteSpace(entry.ScreenshotPath))
            {
                builder.Append(" | Screenshot=").Append(NormalizeText(entry.ScreenshotPath));
            }

            if (!string.IsNullOrWhiteSpace(entry.ExceptionType))
            {
                builder.Append(" | ExceptionType=").Append(entry.ExceptionType);
            }

            return builder.ToString();
        }

        private static string NormalizeText(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", " | ")
                .Replace("\n", " | ")
                .Replace("\r", " | ");
        }

        private static string SanitizeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '_');
            }

            return value;
        }
    }
}
