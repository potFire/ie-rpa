using System;
using System.IO;
using System.Text;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public class InMemoryLogService : ILogService
    {
        private readonly object _syncRoot = new object();
        private readonly string _baseDirectory;
        private string _currentLogFilePath;

        public InMemoryLogService()
        {
            _baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            ResetSessionFile();
        }

        public event EventHandler<ExecutionLogEntry> EntryAdded;

        public void Clear()
        {
            lock (_syncRoot)
            {
                ResetSessionFile();
            }
        }

        public void Log(LogLevel level, string message, WorkflowStep step = null, string screenshotPath = null)
        {
            var entry = new ExecutionLogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                StepId = step != null ? step.Id : null,
                StepName = step != null ? step.Name : null,
                Message = message,
                ScreenshotPath = screenshotPath
            };

            lock (_syncRoot)
            {
                AppendEntryToFile(entry);
            }

            var handler = EntryAdded;
            if (handler != null)
            {
                handler(this, entry);
            }
        }

        private void ResetSessionFile()
        {
            var directory = Path.Combine(_baseDirectory, DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(directory);
            _currentLogFilePath = Path.Combine(directory, "run_" + DateTime.Now.ToString("HHmmssfff") + ".log");
            File.WriteAllText(_currentLogFilePath, "", Encoding.UTF8);
        }

        private void AppendEntryToFile(ExecutionLogEntry entry)
        {
            var line = string.Format(
                "[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] [Step:{2}] {3}",
                entry.Timestamp,
                entry.Level,
                string.IsNullOrWhiteSpace(entry.StepName) ? "-" : entry.StepName,
                entry.Message ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(entry.ScreenshotPath))
            {
                line += " | Screenshot=" + entry.ScreenshotPath;
            }

            File.AppendAllText(_currentLogFilePath, line + Environment.NewLine, Encoding.UTF8);
        }
    }
}
