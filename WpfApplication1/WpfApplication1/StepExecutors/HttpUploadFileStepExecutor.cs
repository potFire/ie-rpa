using System;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class HttpUploadFileStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;
        private readonly IHttpFileUploadService _uploadService;

        public HttpUploadFileStepExecutor(IVariableResolver variableResolver, IHttpFileUploadService uploadService)
        {
            _variableResolver = variableResolver;
            _uploadService = uploadService;
        }

        public StepType StepType
        {
            get { return StepType.HttpUploadFile; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string url;
            string filePath;
            string responseVariableName;
            step.Parameters.TryGetValue("url", out url);
            step.Parameters.TryGetValue("filePath", out filePath);
            step.Parameters.TryGetValue("responseVariableName", out responseVariableName);

            url = _variableResolver.ResolveString(url, context);
            filePath = _variableResolver.ResolveString(filePath, context);
            responseVariableName = _variableResolver.ResolveString(responseVariableName, context);

            if (string.IsNullOrWhiteSpace(url))
            {
                return StepExecutionResult.Failure("未配置上传 URL。");
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return StepExecutionResult.Failure("未配置待上传文件路径。");
            }

            var timeoutMs = step.TimeoutMs > 0 ? step.TimeoutMs : 10000;
            var result = await _uploadService.UploadAsync(url, filePath, timeoutMs, context.CancellationToken);
            if (!string.IsNullOrWhiteSpace(responseVariableName))
            {
                context.Variables[responseVariableName] = result.ResponseText ?? string.Empty;
            }

            var state = context.CurrentBusinessState;
            if (state != null)
            {
                state.HtmlFilePath = filePath;
                state.UploadResult = result.ResponseText;
                state.Stage = result.IsSuccess ? BusinessStateStage.Uploaded : BusinessStateStage.Failed;
                state.UploadedAt = result.IsSuccess ? DateTime.Now : state.UploadedAt;
                state.ErrorMessage = result.IsSuccess ? string.Empty : result.ResponseText;
                state.IsCompleted = result.IsSuccess;
                BusinessStateSupport.SyncVariables(context);
                await BusinessStateSupport.PersistAsync(context);
            }

            if (!result.IsSuccess)
            {
                return StepExecutionResult.Failure("HTTP 文件上传失败：" + (result.ResponseText ?? string.Empty));
            }

            return StepExecutionResult.Success("HTTP 文件上传完成，状态码：" + result.StatusCode);
        }
    }
}