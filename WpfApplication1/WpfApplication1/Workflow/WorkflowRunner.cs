using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;

namespace WpfApplication1.Workflow
{
    public class WorkflowRunner : IWorkflowRunner
    {
        private readonly StepExecutorFactory _stepExecutorFactory;
        private readonly ILogService _logService;
        private readonly IDesktopInteractionService _desktopInteractionService;

        public WorkflowRunner(
            StepExecutorFactory stepExecutorFactory,
            ILogService logService,
            IDesktopInteractionService desktopInteractionService)
        {
            _stepExecutorFactory = stepExecutorFactory;
            _logService = logService;
            _desktopInteractionService = desktopInteractionService;
        }

        public async Task RunAsync(WorkflowDefinition workflow, IExecutionContext context, int startStepIndex = 0, int? maxSteps = null)
        {
            if (workflow == null)
            {
                throw new ArgumentNullException("workflow");
            }

            if (startStepIndex < 0 || startStepIndex >= workflow.Steps.Count)
            {
                throw new ArgumentOutOfRangeException("startStepIndex", "Start step index is out of range.");
            }

            context.UpdateRuntimeState(state =>
            {
                state.CurrentWorkflowName = workflow.Name ?? string.Empty;
                state.CurrentWorkflowPath = context.CurrentWorkflowPath ?? string.Empty;
                state.RecentErrorSummary = string.Empty;
            });

            PrepareLoopPairs(workflow, context);
            _logService.Log(LogLevel.Info, BuildWorkflowStartMessage(workflow, startStepIndex, maxSteps), null, null, context, "WorkflowStarted");

            var executedSteps = 0;
            for (var index = startStepIndex; index < workflow.Steps.Count; index++)
            {
                if (maxSteps.HasValue && executedSteps >= maxSteps.Value)
                {
                    break;
                }

                var step = workflow.Steps[index];
                context.CancellationToken.ThrowIfCancellationRequested();
                UpdateStepRuntimeState(context, step);

                var attempts = Math.Max(1, step.RetryCount + 1);
                StepExecutionResult result = null;

                for (var attempt = 1; attempt <= attempts; attempt++)
                {
                    var executor = _stepExecutorFactory.GetExecutor(step.StepType);
                    var stopwatch = Stopwatch.StartNew();
                    _logService.Log(LogLevel.Info, BuildStepStartMessage(step, attempt, attempts), step, null, context, "StepStarted", attempt, attempts);

                    try
                    {
                        result = await executor.ExecuteAsync(step, context);
                        stopwatch.Stop();

                        if (result != null)
                        {
                            result.Duration = stopwatch.Elapsed;
                        }

                        RefreshPageRuntimeState(context);

                        if (result == null || result.IsSuccess)
                        {
                            var successMessage = BuildStepSuccessMessage(step, result, context);
                            _logService.Log(LogLevel.Info, successMessage, step, null, context, "StepSucceeded", attempt, attempts);
                            context.UpdateRuntimeState(state => state.RecentErrorSummary = string.Empty);
                            break;
                        }

                        var screenshotPath = attempt == attempts ? CaptureFailureScreenshot(step) : null;
                        context.UpdateRuntimeState(state => state.RecentErrorSummary = result.Message ?? string.Empty);
                        _logService.Log(
                            LogLevel.Warning,
                            BuildStepFailureMessage(step, result, attempt, attempts),
                            step,
                            screenshotPath,
                            context,
                            "StepFailed",
                            attempt,
                            attempts,
                            result.Exception);
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        result = StepExecutionResult.Failure(ex.Message, ex);
                        var screenshotPath = attempt == attempts ? CaptureFailureScreenshot(step) : null;
                        context.UpdateRuntimeState(state => state.RecentErrorSummary = ex.Message);
                        _logService.Log(
                            LogLevel.Error,
                            BuildStepCrashMessage(step, ex, attempt, attempts),
                            step,
                            screenshotPath,
                            context,
                            "StepCrashed",
                            attempt,
                            attempts,
                            ex);
                    }
                }

                executedSteps++;

                if (result == null)
                {
                    continue;
                }

                if (!result.IsSuccess && !step.ContinueOnError)
                {
                    throw new InvalidOperationException(
                        string.Format("Step '{0}' failed: {1}", step.Name, result.Message),
                        result.Exception);
                }

                if (result.IsSuccess && result.NextStepIndex.HasValue)
                {
                    var nextStepIndex = result.NextStepIndex.Value;
                    if (nextStepIndex < 0 || nextStepIndex >= workflow.Steps.Count)
                    {
                        throw new InvalidOperationException("Workflow jump target is out of range: " + nextStepIndex);
                    }

                    index = nextStepIndex - 1;
                }
            }

            _logService.Log(LogLevel.Info, BuildWorkflowCompletedMessage(workflow, executedSteps), null, null, context, "WorkflowCompleted");
        }

        private static string BuildWorkflowStartMessage(WorkflowDefinition workflow, int startStepIndex, int? maxSteps)
        {
            return string.Format("开始执行流程：{0}，起始步骤索引={1}，最大执行步数={2}。",
                workflow.Name ?? string.Empty,
                startStepIndex,
                maxSteps.HasValue ? maxSteps.Value.ToString() : "不限");
        }

        private static string BuildWorkflowCompletedMessage(WorkflowDefinition workflow, int executedSteps)
        {
            return string.Format("流程执行结束：{0}，本次共执行 {1} 个步骤。", workflow.Name ?? string.Empty, executedSteps);
        }

        private static string BuildStepStartMessage(WorkflowStep step, int attempt, int maxAttempts)
        {
            return string.Format("开始执行步骤：{0}（类型={1}，第 {2}/{3} 次尝试）。",
                step != null ? step.Name : string.Empty,
                step != null ? step.StepType.ToString() : string.Empty,
                attempt,
                maxAttempts);
        }

        private static string BuildStepFailureMessage(WorkflowStep step, StepExecutionResult result, int attempt, int maxAttempts)
        {
            return string.Format("步骤执行失败：{0}（第 {1}/{2} 次尝试）。原因：{3}",
                step != null ? step.Name : string.Empty,
                attempt,
                maxAttempts,
                result != null ? result.Message : string.Empty);
        }

        private static string BuildStepCrashMessage(WorkflowStep step, Exception exception, int attempt, int maxAttempts)
        {
            return string.Format("步骤执行异常：{0}（第 {1}/{2} 次尝试）。异常：{3}",
                step != null ? step.Name : string.Empty,
                attempt,
                maxAttempts,
                exception != null ? exception.Message : string.Empty);
        }

        private static string BuildStepSuccessMessage(WorkflowStep step, StepExecutionResult result, IExecutionContext context)
        {
            var baseMessage = result != null && !string.IsNullOrWhiteSpace(result.Message)
                ? result.Message
                : "步骤执行成功。";
            var detail = BuildStepDetailSummary(step, context);
            return string.IsNullOrWhiteSpace(detail) ? baseMessage : baseMessage + " | " + detail;
        }

        private static string BuildStepDetailSummary(WorkflowStep step, IExecutionContext context)
        {
            if (step == null || context == null)
            {
                return string.Empty;
            }

            switch (step.StepType)
            {
                case StepType.HttpGetData:
                    return string.Format("hasData={0}; data={1}",
                        PreviewVariable(context, SafeGetParameter(step, "hasDataVariableName", "HasApiData")),
                        PreviewVariable(context, SafeGetParameter(step, "dataVariableName", "ApiData")));
                case StepType.SetVariable:
                    return string.Format("{0}={1}",
                        SafeGetParameter(step, "name", "-"),
                        PreviewVariable(context, SafeGetParameter(step, "name", string.Empty)));
                case StepType.ReadText:
                    return string.Format("{0}={1}",
                        SafeGetParameter(step, "variableName", "LastReadText"),
                        PreviewVariable(context, SafeGetParameter(step, "variableName", "LastReadText")));
                case StepType.QueryAndExportReport:
                    return string.Format("report={0}; upload={1}",
                        PreviewVariable(context, "LastReportFilePath"),
                        PreviewVariable(context, "LastUploadResponse"));
                case StepType.PageListLoop:
                    return string.Format("scan={0}; handled={1}; pending={2}; mode={3}",
                        PreviewVariable(context, "LastScanCount"),
                        PreviewVariable(context, "LastHandledCount"),
                        PreviewVariable(context, "LastPendingCount"),
                        PreviewVariable(context, "LastRunMode"));
                default:
                    return string.Empty;
            }
        }

        private static string SafeGetParameter(WorkflowStep step, string key, string defaultValue)
        {
            if (step == null || step.Parameters == null || string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            string value;
            return step.Parameters.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : defaultValue;
        }

        private static string PreviewVariable(IExecutionContext context, string variableName)
        {
            if (context == null || string.IsNullOrWhiteSpace(variableName) || context.Variables == null)
            {
                return string.Empty;
            }

            object value;
            if (!context.Variables.TryGetValue(variableName, out value) || value == null)
            {
                return string.Empty;
            }

            var text = Convert.ToString(value) ?? string.Empty;
            if (text.Length > 180)
            {
                text = text.Substring(0, 180) + "...";
            }

            return text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        }

        private static void UpdateStepRuntimeState(IExecutionContext context, WorkflowStep step)
        {
            if (context == null || step == null)
            {
                return;
            }

            context.UpdateRuntimeState(state =>
            {
                state.CurrentStepId = step.Id ?? string.Empty;
                state.CurrentStepName = step.Name ?? string.Empty;
                state.CurrentMode = context.SchedulerMode ?? state.CurrentMode ?? string.Empty;
                var businessState = context.CurrentBusinessState;
                state.CurrentObject = businessState == null
                    ? string.Empty
                    : string.Format("{0} / {1}", businessState.Name ?? string.Empty, businessState.IdCardNumber ?? string.Empty).Trim(' ', '/');
            });

            RefreshPageRuntimeState(context);
        }

        private static void RefreshPageRuntimeState(IExecutionContext context)
        {
            if (context == null)
            {
                return;
            }

            var page = context.CurrentPage as IIePage;
            context.UpdateRuntimeState(state =>
            {
                state.CurrentWindowTitle = page != null ? (page.Title ?? string.Empty) : string.Empty;
                state.CurrentPageUrl = page != null ? (page.Url ?? string.Empty) : string.Empty;
                state.FramePathDisplay = page != null ? (page.FramePathDisplay ?? "root") : "root";
                state.FrameDepth = page != null ? page.FrameDepth : 0;
            });
        }

        private static void PrepareLoopPairs(WorkflowDefinition workflow, IExecutionContext context)
        {
            context.LoopPairs.Clear();
            context.LoopStates.Clear();

            var pendingStarts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < workflow.Steps.Count; index++)
            {
                var step = workflow.Steps[index];
                if (step == null)
                {
                    continue;
                }

                if (step.StepType == StepType.LoopStart)
                {
                    var loopKey = GetLoopKey(step, "loopStart");
                    if (pendingStarts.ContainsKey(loopKey) || context.LoopPairs.ContainsKey(loopKey))
                    {
                        throw new InvalidOperationException("Duplicate loop key: " + loopKey);
                    }

                    pendingStarts[loopKey] = index;
                }
                else if (step.StepType == StepType.LoopEnd)
                {
                    var loopKey = GetLoopKey(step, "loopEnd");
                    int startIndex;
                    if (!pendingStarts.TryGetValue(loopKey, out startIndex))
                    {
                        throw new InvalidOperationException("Missing loop start for key: " + loopKey);
                    }

                    context.LoopPairs[loopKey] = new LoopPairInfo
                    {
                        LoopKey = loopKey,
                        StartStepIndex = startIndex,
                        EndStepIndex = index
                    };
                    pendingStarts.Remove(loopKey);
                }
            }

            if (pendingStarts.Count > 0)
            {
                throw new InvalidOperationException("There are unclosed loops: " + string.Join(", ", pendingStarts.Keys));
            }
        }

        private static string GetLoopKey(WorkflowStep step, string stepName)
        {
            string loopKey;
            if (!step.Parameters.TryGetValue("loopKey", out loopKey) || string.IsNullOrWhiteSpace(loopKey))
            {
                throw new InvalidOperationException(stepName + " is missing loopKey.");
            }

            return loopKey.Trim();
        }

        private string CaptureFailureScreenshot(WorkflowStep step)
        {
            if (_desktopInteractionService == null)
            {
                return null;
            }

            try
            {
                var prefix = string.IsNullOrWhiteSpace(step != null ? step.Name : null)
                    ? "step_failure"
                    : SanitizeFileName(step.Name);
                var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots", "Failures");
                return _desktopInteractionService.CaptureDesktop(null, directory, prefix);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, "Auto screenshot failed: " + ex.Message, step, null, null, "ScreenshotFailed", 0, 0, ex);
                return null;
            }
        }

        private static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "step_failure";
            }

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(invalidChar, '_');
            }

            return raw;
        }
    }
}
