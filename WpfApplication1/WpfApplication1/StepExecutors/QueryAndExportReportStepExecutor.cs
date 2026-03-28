using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class QueryAndExportReportStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;
        private readonly IHttpFileUploadService _uploadService;

        public QueryAndExportReportStepExecutor(IVariableResolver variableResolver, IHttpFileUploadService uploadService)
        {
            _variableResolver = variableResolver;
            _uploadService = uploadService;
        }

        public StepType StepType
        {
            get { return StepType.QueryAndExportReport; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = CompositeIeStepHelper.ResolvePage(context, "当前没有可用的 IE 页面，无法查询并导出报告。");
            var rowIndex = ResolveCurrentRowIndex(context);
            var queryButtonSelector = Resolve(step, context, "queryButtonSelector", rowIndex);
            var popupReadySelector = Resolve(step, context, "popupReadySelector", rowIndex);
            var reportIframeSelector = Resolve(step, context, "reportIframeSelector", rowIndex);
            var saveDirectory = Resolve(step, context, "saveDirectory", rowIndex);
            var fileNameTemplate = Resolve(step, context, "fileNameTemplate", rowIndex);
            var uploadUrl = Resolve(step, context, "uploadUrl", rowIndex);
            var closePopupSelector = Resolve(step, context, "closePopupSelector", rowIndex);
            var outputFileVariableName = Resolve(step, context, "outputFileVariableName", rowIndex);
            var uploadResponseVariableName = Resolve(step, context, "uploadResponseVariableName", rowIndex);
            var pollIntervalMs = CompositeIeStepHelper.ResolvePositiveInt(Resolve(step, context, "popupPollIntervalMs", rowIndex), 500);

            if (!string.IsNullOrWhiteSpace(queryButtonSelector))
            {
                CompositeIeStepHelper.ClickSelector(page, queryButtonSelector);
            }

            if (!string.IsNullOrWhiteSpace(popupReadySelector))
            {
                await CompositeIeStepHelper.WaitForElementAsync(page, popupReadySelector, step.TimeoutMs > 0 ? step.TimeoutMs : 10000, pollIntervalMs, context.CancellationToken);
            }

            if (string.IsNullOrWhiteSpace(reportIframeSelector))
            {
                return StepExecutionResult.Failure("未配置报告 iframe 选择器。");
            }

            var reportPage = page.EnterFrame(WpfApplication1.Selectors.SelectorParser.Parse(CompositeIeStepHelper.NormalizeSelector(reportIframeSelector)));
            var html = reportPage.GetHtml();
            var finalDirectory = CompositeIeStepHelper.EnsureDirectory(saveDirectory);
            var finalFileName = BuildFileName(fileNameTemplate, context, rowIndex);
            var finalPath = Path.Combine(finalDirectory, finalFileName);
            File.WriteAllText(finalPath, html ?? string.Empty, new UTF8Encoding(false));

            context.Variables["LastReportFilePath"] = finalPath;
            context.Variables["LastHandledCount"] = 1;
            context.Variables["LastRunMode"] = "queryReport";
            if (!string.IsNullOrWhiteSpace(outputFileVariableName))
            {
                context.Variables[outputFileVariableName] = finalPath;
            }

            var state = context.CurrentBusinessState;
            if (state != null)
            {
                state.HtmlFilePath = finalPath;
                state.Stage = BusinessStateStage.ReportSaved;
                state.ErrorMessage = string.Empty;
            }

            string uploadMessage = string.Empty;
            if (!string.IsNullOrWhiteSpace(uploadUrl))
            {
                var uploadResult = await _uploadService.UploadAsync(uploadUrl, finalPath, step.TimeoutMs > 0 ? step.TimeoutMs : 10000, context.CancellationToken);
                uploadMessage = uploadResult.ResponseText ?? string.Empty;
                context.Variables["LastUploadResponse"] = uploadMessage;
                if (!string.IsNullOrWhiteSpace(uploadResponseVariableName))
                {
                    context.Variables[uploadResponseVariableName] = uploadMessage;
                }

                if (state != null)
                {
                    state.UploadResult = uploadMessage;
                    state.UploadedAt = uploadResult.IsSuccess ? DateTime.Now : state.UploadedAt;
                    state.Stage = uploadResult.IsSuccess ? BusinessStateStage.Uploaded : BusinessStateStage.Failed;
                    state.ErrorMessage = uploadResult.IsSuccess ? string.Empty : uploadMessage;
                    state.IsCompleted = uploadResult.IsSuccess;
                }

                if (!uploadResult.IsSuccess)
                {
                    BusinessStateSupport.SyncVariables(context);
                    await BusinessStateSupport.PersistAsync(context);
                    context.Variables["LastPendingCount"] = 1;
                    return StepExecutionResult.Failure("报告文件上传失败：" + uploadMessage);
                }
            }

            if (!string.IsNullOrWhiteSpace(closePopupSelector))
            {
                CompositeIeStepHelper.ClickSelector(page, closePopupSelector);
            }

            context.Variables["LastPendingCount"] = 0;
            BusinessStateSupport.SyncVariables(context);
            await BusinessStateSupport.PersistAsync(context);
            return StepExecutionResult.Success("报告已导出为 HTML 文件：" + finalPath);
        }

        private string Resolve(WorkflowStep step, IExecutionContext context, string key, int rowIndex)
        {
            string raw;
            step.Parameters.TryGetValue(key, out raw);
            return CompositeIeStepHelper.ResolveTemplate(raw, _variableResolver, context, rowIndex);
        }

        private static int ResolveCurrentRowIndex(IExecutionContext context)
        {
            object value;
            if (context.Variables.TryGetValue("CurrentRowIndex", out value) && value != null)
            {
                int rowIndex;
                if (int.TryParse(Convert.ToString(value), out rowIndex))
                {
                    return rowIndex;
                }
            }

            return 0;
        }

        private static string BuildFileName(string fileNameTemplate, IExecutionContext context, int rowIndex)
        {
            var fileName = fileNameTemplate;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                var businessName = context.Variables.ContainsKey("BusinessName") ? Convert.ToString(context.Variables["BusinessName"]) : string.Empty;
                fileName = string.Format("report_{0}_{1}_{2}.html",
                    CompositeIeStepHelper.SanitizeFileName(string.IsNullOrWhiteSpace(businessName) ? "business" : businessName),
                    rowIndex > 0 ? rowIndex.ToString() : "single",
                    DateTime.Now.ToString("yyyyMMdd_HHmmssfff"));
            }

            fileName = CompositeIeStepHelper.SanitizeFileName(fileName);
            if (!fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".html";
            }

            return fileName;
        }
    }
}
