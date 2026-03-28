using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public class WorkflowLogService : IWorkflowLogService
    {
        private readonly string _logsRootDirectory;

        public WorkflowLogService()
        {
            _logsRootDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(_logsRootDirectory);
        }

        public Task<IList<WorkflowLogWorkflowItem>> GetLoggedWorkflowsAsync()
        {
            var result = new List<WorkflowLogWorkflowItem>();
            if (!Directory.Exists(_logsRootDirectory))
            {
                return Task.FromResult<IList<WorkflowLogWorkflowItem>>(result);
            }

            foreach (var workflowDirectory in Directory.GetDirectories(_logsRootDirectory))
            {
                var workflowId = Path.GetFileName(workflowDirectory);
                if (string.Equals(workflowId, "Application", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var runs = GetRunsInternal(workflowId, workflowDirectory);
                if (runs.Count == 0)
                {
                    continue;
                }

                var latest = runs.OrderByDescending(item => item.StartedAt).First();
                result.Add(new WorkflowLogWorkflowItem
                {
                    WorkflowId = workflowId,
                    WorkflowName = latest.WorkflowName,
                    WorkflowType = latest.WorkflowType,
                    LastRunAt = latest.StartedAt,
                    LastResult = latest.Result,
                    RunCount = runs.Count
                });
            }

            return Task.FromResult<IList<WorkflowLogWorkflowItem>>(result.OrderByDescending(item => item.LastRunAt).ToList());
        }

        public Task<IList<WorkflowLogRunItem>> GetRunsAsync(string workflowId)
        {
            if (string.IsNullOrWhiteSpace(workflowId))
            {
                return Task.FromResult<IList<WorkflowLogRunItem>>(new List<WorkflowLogRunItem>());
            }

            var workflowDirectory = Path.Combine(_logsRootDirectory, workflowId);
            return Task.FromResult<IList<WorkflowLogRunItem>>(GetRunsInternal(workflowId, workflowDirectory)
                .OrderByDescending(item => item.StartedAt)
                .ToList());
        }

        public Task<string> LoadRunTextAsync(WorkflowLogRunItem runItem)
        {
            if (runItem == null || string.IsNullOrWhiteSpace(runItem.LogFilePath) || !File.Exists(runItem.LogFilePath))
            {
                return Task.FromResult(string.Empty);
            }

            return Task.FromResult(File.ReadAllText(runItem.LogFilePath, Encoding.UTF8));
        }

        public Task ExportRunAsync(WorkflowLogRunItem runItem, string exportPath)
        {
            if (runItem == null)
            {
                throw new ArgumentNullException("runItem");
            }

            if (string.IsNullOrWhiteSpace(runItem.LogFilePath) || !File.Exists(runItem.LogFilePath))
            {
                throw new FileNotFoundException("Run log file was not found.", runItem.LogFilePath);
            }

            var directory = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(runItem.LogFilePath, exportPath, true);
            return Task.FromResult(0);
        }

        private static IList<WorkflowLogRunItem> GetRunsInternal(string workflowId, string workflowDirectory)
        {
            var result = new List<WorkflowLogRunItem>();
            if (string.IsNullOrWhiteSpace(workflowDirectory) || !Directory.Exists(workflowDirectory))
            {
                return result;
            }

            foreach (var file in Directory.GetFiles(workflowDirectory, "*.log", SearchOption.AllDirectories))
            {
                WorkflowLogRunItem item;
                if (TryParseRun(file, workflowId, out item))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private static bool TryParseRun(string filePath, string workflowId, out WorkflowLogRunItem runItem)
        {
            runItem = null;
            try
            {
                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = new StreamReader(filePath, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!line.StartsWith("# "))
                        {
                            break;
                        }

                        if (string.Equals(line, "# -- entries --", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        var separatorIndex = line.IndexOf(':');
                        if (separatorIndex <= 2)
                        {
                            continue;
                        }

                        var key = line.Substring(2, separatorIndex - 2).Trim();
                        var value = line.Substring(separatorIndex + 1).Trim();
                        metadata[key] = value;
                    }
                }

                if (metadata.Count == 0)
                {
                    return false;
                }

                WorkflowType workflowType;
                Enum.TryParse(metadata.ContainsKey("WorkflowType") ? metadata["WorkflowType"] : null, true, out workflowType);
                DateTime startedAt;
                DateTime.TryParse(metadata.ContainsKey("StartedAt") ? metadata["StartedAt"] : null, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out startedAt);
                DateTime endedAt;
                DateTime.TryParse(metadata.ContainsKey("EndedAt") ? metadata["EndedAt"] : null, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out endedAt);

                runItem = new WorkflowLogRunItem
                {
                    WorkflowId = metadata.ContainsKey("WorkflowId") && !string.IsNullOrWhiteSpace(metadata["WorkflowId"]) ? metadata["WorkflowId"] : workflowId,
                    WorkflowName = metadata.ContainsKey("WorkflowName") ? metadata["WorkflowName"] : workflowId,
                    WorkflowType = workflowType,
                    WorkflowPath = metadata.ContainsKey("WorkflowPath") ? metadata["WorkflowPath"] : string.Empty,
                    RunId = metadata.ContainsKey("RunId") ? metadata["RunId"] : Path.GetFileNameWithoutExtension(filePath),
                    RunName = metadata.ContainsKey("RunName") ? metadata["RunName"] : string.Empty,
                    RunMode = metadata.ContainsKey("RunMode") ? metadata["RunMode"] : string.Empty,
                    Result = metadata.ContainsKey("Result") ? metadata["Result"] : string.Empty,
                    Summary = metadata.ContainsKey("Summary") ? metadata["Summary"] : string.Empty,
                    StartedAt = startedAt == default(DateTime) ? File.GetCreationTime(filePath) : startedAt,
                    EndedAt = endedAt == default(DateTime) ? (DateTime?)null : endedAt,
                    LogFilePath = filePath
                };
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
