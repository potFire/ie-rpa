using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
                throw new ArgumentOutOfRangeException("startStepIndex", "起始步骤索引超出范围。") ;
            }

            var executedSteps = 0;
            for (var index = startStepIndex; index < workflow.Steps.Count; index++)
            {
                if (maxSteps.HasValue && executedSteps >= maxSteps.Value)
                {
                    break;
                }

                var step = workflow.Steps[index];
                context.CancellationToken.ThrowIfCancellationRequested();

                var attempts = Math.Max(1, step.RetryCount + 1);
                StepExecutionResult result = null;

                for (var attempt = 1; attempt <= attempts; attempt++)
                {
                    var executor = _stepExecutorFactory.GetExecutor(step.StepType);
                    var stopwatch = Stopwatch.StartNew();
                    _logService.Log(LogLevel.Info, "开始执行。", step);

                    try
                    {
                        result = await executor.ExecuteAsync(step, context);
                        stopwatch.Stop();

                        if (result != null)
                        {
                            result.Duration = stopwatch.Elapsed;
                        }

                        if (result == null || result.IsSuccess)
                        {
                            var successMessage = result != null && !string.IsNullOrWhiteSpace(result.Message)
                                ? result.Message
                                : "执行成功。";
                            _logService.Log(LogLevel.Info, successMessage, step);
                            break;
                        }

                        var screenshotPath = attempt == attempts ? CaptureFailureScreenshot(step) : null;
                        _logService.Log(
                            LogLevel.Warning,
                            string.Format("第 {0}/{1} 次执行失败：{2}", attempt, attempts, result.Message),
                            step,
                            screenshotPath);
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        result = StepExecutionResult.Failure(ex.Message, ex);
                        var screenshotPath = attempt == attempts ? CaptureFailureScreenshot(step) : null;
                        _logService.Log(
                            LogLevel.Error,
                            string.Format("第 {0}/{1} 次执行异常：{2}", attempt, attempts, ex.Message),
                            step,
                            screenshotPath);
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
                        string.Format("步骤“{0}”执行失败：{1}", step.Name, result.Message),
                        result.Exception);
                }

                // 条件判断和循环步骤会通过 NextStepIndex 把执行位置跳到别的步骤。
                // 这里统一处理跳转逻辑，避免把流程控制散落在运行器外面。
                if (result.IsSuccess && result.NextStepIndex.HasValue)
                {
                    var nextStepIndex = result.NextStepIndex.Value;
                    if (nextStepIndex < 0 || nextStepIndex >= workflow.Steps.Count)
                    {
                        throw new InvalidOperationException("流程跳转目标越界：" + nextStepIndex);
                    }

                    index = nextStepIndex - 1;
                }
            }
        }

        private string CaptureFailureScreenshot(WorkflowStep step)
        {
            if (_desktopInteractionService == null)
            {
                return null;
            }

            try
            {
                // 失败截图统一落到 Failures 目录，便于实施人员集中排查。
                // 文件名前缀带上步骤名，这样看目录时能快速知道是哪一步出的错。
                var prefix = string.IsNullOrWhiteSpace(step != null ? step.Name : null)
                    ? "step_failure"
                    : SanitizeFileName(step.Name);
                var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots", "Failures");
                return _desktopInteractionService.CaptureDesktop(null, directory, prefix);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, "自动截图失败：" + ex.Message, step);
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
