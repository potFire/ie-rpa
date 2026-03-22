using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class WaitDownloadStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public WaitDownloadStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.WaitDownload; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string downloadDirectory;
            string fileName;
            string filePattern;
            string stableMsRaw;
            string outputVariableName;

            step.Parameters.TryGetValue("downloadDirectory", out downloadDirectory);
            step.Parameters.TryGetValue("fileName", out fileName);
            step.Parameters.TryGetValue("filePattern", out filePattern);
            step.Parameters.TryGetValue("stableMs", out stableMsRaw);
            step.Parameters.TryGetValue("outputVariableName", out outputVariableName);

            downloadDirectory = _variableResolver.ResolveString(downloadDirectory, context);
            fileName = _variableResolver.ResolveString(fileName, context);
            filePattern = _variableResolver.ResolveString(filePattern, context);
            outputVariableName = _variableResolver.ResolveString(outputVariableName, context);

            if (string.IsNullOrWhiteSpace(downloadDirectory))
            {
                downloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }

            if (!Directory.Exists(downloadDirectory))
            {
                return StepExecutionResult.Failure("下载目录不存在：" + downloadDirectory);
            }

            var stableMs = 1200;
            int parsedStableMs;
            if (int.TryParse(stableMsRaw, out parsedStableMs) && parsedStableMs > 0)
            {
                stableMs = parsedStableMs;
            }

            var startedAt = DateTime.UtcNow;
            while ((DateTime.UtcNow - startedAt).TotalMilliseconds < step.TimeoutMs)
            {
                var file = FindDownloadFile(downloadDirectory, fileName, filePattern, startedAt);
                if (file != null && IsFileStable(file, stableMs))
                {
                    if (!string.IsNullOrWhiteSpace(outputVariableName))
                    {
                        context.Variables[outputVariableName] = file.FullName;
                    }

                    return StepExecutionResult.Success("下载文件已就绪：" + file.FullName);
                }

                await Task.Delay(300);
            }

            return StepExecutionResult.Failure("等待下载完成超时。目录：" + downloadDirectory);
        }

        private static FileInfo FindDownloadFile(string directory, string fileName, string filePattern, DateTime startedAt)
        {
            var dirInfo = new DirectoryInfo(directory);
            var files = dirInfo.GetFiles()
                .Where(file => !IsTemporaryDownloadFile(file.Name))
                .Where(file => file.LastWriteTime >= startedAt.AddMinutes(-2))
                .OrderByDescending(file => file.LastWriteTime)
                .ToList();

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return files.FirstOrDefault(file => string.Equals(file.Name, fileName, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(filePattern))
            {
                return files.FirstOrDefault(file => file.Name.IndexOf(filePattern, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return files.FirstOrDefault();
        }

        private static bool IsFileStable(FileInfo file, int stableMs)
        {
            try
            {
                var firstLength = file.Length;
                System.Threading.Thread.Sleep(stableMs);
                file.Refresh();
                return file.Exists && file.Length == firstLength;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTemporaryDownloadFile(string fileName)
        {
            var lower = (fileName ?? string.Empty).ToLowerInvariant();
            return lower.EndsWith(".crdownload")
                   || lower.EndsWith(".part")
                   || lower.EndsWith(".tmp");
        }
    }
}
