using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class ScreenshotStepExecutor : IStepExecutor
    {
        private readonly IDesktopInteractionService _desktopInteractionService;
        private readonly IVariableResolver _variableResolver;

        public ScreenshotStepExecutor(IDesktopInteractionService desktopInteractionService, IVariableResolver variableResolver)
        {
            _desktopInteractionService = desktopInteractionService;
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.Screenshot; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string outputPath;
            string directory;
            string fileNamePrefix;
            string outputVariableName;

            step.Parameters.TryGetValue("outputPath", out outputPath);
            step.Parameters.TryGetValue("directory", out directory);
            step.Parameters.TryGetValue("fileNamePrefix", out fileNamePrefix);
            step.Parameters.TryGetValue("outputVariableName", out outputVariableName);

            outputPath = _variableResolver.ResolveString(outputPath, context);
            directory = _variableResolver.ResolveString(directory, context);
            fileNamePrefix = _variableResolver.ResolveString(fileNamePrefix, context);
            outputVariableName = _variableResolver.ResolveString(outputVariableName, context);

            // 第一版先截整个桌面，这样无论是浏览器页面、系统弹窗还是上传对话框，
            // 都能被完整保留下来，排查问题最直接。
            var finalPath = _desktopInteractionService.CaptureDesktop(outputPath, directory, fileNamePrefix);
            if (!string.IsNullOrWhiteSpace(outputVariableName))
            {
                context.Variables[outputVariableName] = finalPath;
            }

            return Task.FromResult(StepExecutionResult.Success("截图已保存：" + finalPath));
        }
    }
}
