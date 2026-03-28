using System;
using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class PageListLoopStepExecutor : IStepExecutor
    {
        private readonly IIeBrowserService _browserService;
        private readonly IDesktopInteractionService _desktopInteractionService;
        private readonly IVariableResolver _variableResolver;
        private readonly QueryAndExportReportStepExecutor _queryAndExportReportStepExecutor;

        public PageListLoopStepExecutor(
            IIeBrowserService browserService,
            IDesktopInteractionService desktopInteractionService,
            IVariableResolver variableResolver,
            IHttpFileUploadService uploadService)
        {
            _browserService = browserService;
            _desktopInteractionService = desktopInteractionService;
            _variableResolver = variableResolver;
            _queryAndExportReportStepExecutor = new QueryAndExportReportStepExecutor(variableResolver, uploadService);
        }

        public StepType StepType
        {
            get { return StepType.PageListLoop; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = CompositeIeStepHelper.ResolvePage(context, "当前没有可用的 IE 页面，无法执行页面列表循环。");
            var mode = Resolve(step, context, "mode", 0);
            if (string.IsNullOrWhiteSpace(mode))
            {
                mode = "approve";
            }

            var filterSelector = Resolve(step, context, "filterSelector", 0);
            var filterValue = Resolve(step, context, "filterValue", 0);
            var queryButtonSelector = Resolve(step, context, "queryButtonSelector", 0);
            var listReadySelector = Resolve(step, context, "listReadySelector", 0);
            var rowSelectorTemplate = Resolve(step, context, "rowSelectorTemplate", 0);
            var rowActionSelectorTemplate = Resolve(step, context, "rowActionSelectorTemplate", 0);
            var maxRows = CompositeIeStepHelper.ResolvePositiveInt(Resolve(step, context, "maxRows", 0), 50);
            var pollIntervalMs = CompositeIeStepHelper.ResolvePositiveInt(Resolve(step, context, "pollIntervalMs", 0), 500);
            var maxRounds = CompositeIeStepHelper.ResolvePositiveInt(Resolve(step, context, "maxRounds", 0), 100);

            if (string.IsNullOrWhiteSpace(rowSelectorTemplate))
            {
                return StepExecutionResult.Failure("未配置行 XPath 模板 rowSelectorTemplate。");
            }

            if (string.IsNullOrWhiteSpace(rowActionSelectorTemplate))
            {
                return StepExecutionResult.Failure("未配置行内操作 XPath 模板 rowActionSelectorTemplate。");
            }

            var totalScanCount = 0;
            var totalHandledCount = 0;
            for (var round = 1; round <= maxRounds; round++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(filterSelector))
                {
                    CompositeIeStepHelper.SetValue(page, filterSelector, filterValue);
                }

                if (!string.IsNullOrWhiteSpace(queryButtonSelector))
                {
                    CompositeIeStepHelper.ClickSelector(page, queryButtonSelector);
                }

                if (!string.IsNullOrWhiteSpace(listReadySelector))
                {
                    await CompositeIeStepHelper.WaitForElementAsync(page, listReadySelector, step.TimeoutMs > 0 ? step.TimeoutMs : 10000, pollIntervalMs, context.CancellationToken);
                }

                var handled = false;
                var roundScanCount = 0;
                for (var rowIndex = 1; rowIndex <= maxRows; rowIndex++)
                {
                    var rowSelector = CompositeIeStepHelper.ResolveTemplate(rowSelectorTemplate, _variableResolver, context, rowIndex);
                    if (!CompositeIeStepHelper.ElementExists(page, rowSelector))
                    {
                        break;
                    }

                    roundScanCount++;
                    totalScanCount++;
                    var rowActionSelector = CompositeIeStepHelper.ResolveTemplate(rowActionSelectorTemplate, _variableResolver, context, rowIndex);
                    if (!CompositeIeStepHelper.ElementExists(page, rowActionSelector))
                    {
                        continue;
                    }

                    context.Variables["CurrentRowIndex"] = rowIndex;
                    context.Variables["CurrentRowSelector"] = rowSelector;
                    context.Variables["CurrentRowActionSelector"] = rowActionSelector;
                    handled = true;
                    totalHandledCount++;

                    if (string.Equals(mode, "queryreport", StringComparison.OrdinalIgnoreCase))
                    {
                        var childStep = BuildQueryStep(step, rowActionSelector);
                        var childResult = await _queryAndExportReportStepExecutor.ExecuteAsync(childStep, context);
                        if (!childResult.IsSuccess)
                        {
                            FillRuntimeVariables(context, mode, totalScanCount, totalHandledCount, Math.Max(0, roundScanCount - 1));
                            return childResult;
                        }
                    }
                    else
                    {
                        var processResult = await ProcessApprovalLikeActionAsync(step, context, page, rowActionSelector, pollIntervalMs);
                        if (!processResult.IsSuccess)
                        {
                            FillRuntimeVariables(context, mode, totalScanCount, totalHandledCount, Math.Max(0, roundScanCount - 1));
                            return processResult;
                        }
                    }

                    FillRuntimeVariables(context, mode, totalScanCount, totalHandledCount, Math.Max(0, roundScanCount - 1));
                    break;
                }

                if (!handled)
                {
                    FillRuntimeVariables(context, mode, totalScanCount, totalHandledCount, Math.Max(0, roundScanCount));
                    return StepExecutionResult.Success("页面列表循环结束，本轮未发现可处理记录。");
                }
            }

            FillRuntimeVariables(context, mode, totalScanCount, totalHandledCount, Math.Max(0, maxRows - totalHandledCount));
            return StepExecutionResult.Failure("页面列表循环超过最大轮次，已停止以避免无限循环。");
        }

        private async Task<StepExecutionResult> ProcessApprovalLikeActionAsync(WorkflowStep step, IExecutionContext context, IIePage page, string rowActionSelector, int pollIntervalMs)
        {
            var originalPage = context.CurrentPage as IIePage;
            var originalHandle = originalPage != null ? originalPage.WindowHandle : 0;
            var detailReadySelector = Resolve(step, context, "detailReadySelector", 0);
            var detailActionSelector = Resolve(step, context, "detailActionSelector", 0);
            var windowTitle = Resolve(step, context, "targetWindowTitle", 0);
            var windowMatchMode = Resolve(step, context, "windowMatchMode", 0);
            var returnMode = Resolve(step, context, "returnMode", 0);
            var returnSelector = Resolve(step, context, "returnSelector", 0);
            var returnButtonText = Resolve(step, context, "returnButtonText", 0);
            var returnTitleContains = Resolve(step, context, "returnTitleContains", 0);

            CompositeIeStepHelper.ClickSelector(page, rowActionSelector);
            IIePage detailPage = page;
            if (!string.IsNullOrWhiteSpace(windowTitle))
            {
                detailPage = await CompositeIeStepHelper.WaitForWindowAsync(
                    _browserService,
                    originalHandle,
                    windowTitle,
                    windowMatchMode,
                    true,
                    step.TimeoutMs > 0 ? step.TimeoutMs : 10000,
                    pollIntervalMs,
                    context.CancellationToken);
                context.CurrentPage = detailPage;
                context.CurrentBrowser = detailPage;
            }

            if (!string.IsNullOrWhiteSpace(detailReadySelector))
            {
                await CompositeIeStepHelper.WaitForElementAsync(detailPage, detailReadySelector, step.TimeoutMs > 0 ? step.TimeoutMs : 10000, pollIntervalMs, context.CancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(detailActionSelector))
            {
                CompositeIeStepHelper.ClickSelector(detailPage, detailActionSelector);
            }

            if (string.IsNullOrWhiteSpace(returnMode))
            {
                returnMode = string.IsNullOrWhiteSpace(windowTitle) ? "clickSelector" : "closeCurrentWindow";
            }

            if (string.Equals(returnMode, "clickSelector", StringComparison.OrdinalIgnoreCase)
                || string.Equals(returnMode, "clickSelectorAndAlert", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(returnSelector))
                {
                    CompositeIeStepHelper.ClickSelector(detailPage, returnSelector);
                }
            }

            if (string.Equals(returnMode, "alertConfirm", StringComparison.OrdinalIgnoreCase)
                || string.Equals(returnMode, "clickSelectorAndAlert", StringComparison.OrdinalIgnoreCase))
            {
                var handled = _desktopInteractionService.TryHandleDialog(returnButtonText, returnTitleContains, step.TimeoutMs > 0 ? step.TimeoutMs : 10000);
                if (!handled)
                {
                    return StepExecutionResult.Failure("未能处理审批成功弹窗。");
                }
            }

            if (string.Equals(returnMode, "closeCurrentWindow", StringComparison.OrdinalIgnoreCase))
            {
                CompositeIeStepHelper.TryCloseWindow(detailPage);
            }

            if ((string.Equals(returnMode, "closeCurrentWindow", StringComparison.OrdinalIgnoreCase)
                || string.Equals(returnMode, "switchOriginal", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(windowTitle))
                && originalPage != null)
            {
                originalPage.Activate();
                context.CurrentPage = originalPage;
                context.CurrentBrowser = originalPage;
            }

            return StepExecutionResult.Success("当前列表项已处理完成。");
        }

        private static void FillRuntimeVariables(IExecutionContext context, string mode, int scanCount, int handledCount, int pendingCount)
        {
            context.Variables["LastScanCount"] = scanCount;
            context.Variables["LastHandledCount"] = handledCount;
            context.Variables["LastPendingCount"] = pendingCount;
            context.Variables["LastRunMode"] = mode ?? string.Empty;
        }

        private WorkflowStep BuildQueryStep(WorkflowStep parentStep, string queryButtonSelector)
        {
            var childStep = new WorkflowStep
            {
                Id = parentStep.Id + "_queryreport",
                Name = parentStep.Name + "-查询报告",
                StepType = StepType.QueryAndExportReport,
                TimeoutMs = parentStep.TimeoutMs,
                RetryCount = 0,
                ContinueOnError = false,
                Parameters = new StepParameterBag()
            };

            CopyIfExists(parentStep, childStep, "popupReadySelector");
            CopyIfExists(parentStep, childStep, "reportIframeSelector");
            CopyIfExists(parentStep, childStep, "saveDirectory");
            CopyIfExists(parentStep, childStep, "fileNameTemplate");
            CopyIfExists(parentStep, childStep, "uploadUrl");
            CopyIfExists(parentStep, childStep, "closePopupSelector");
            CopyIfExists(parentStep, childStep, "outputFileVariableName");
            CopyIfExists(parentStep, childStep, "uploadResponseVariableName");
            CopyIfExists(parentStep, childStep, "popupPollIntervalMs");
            childStep.Parameters["queryButtonSelector"] = queryButtonSelector;
            return childStep;
        }

        private void CopyIfExists(WorkflowStep source, WorkflowStep target, string parameterName)
        {
            string value;
            if (source.Parameters.TryGetValue(parameterName, out value))
            {
                target.Parameters[parameterName] = value;
            }
        }

        private string Resolve(WorkflowStep step, IExecutionContext context, string key, int rowIndex)
        {
            string raw;
            step.Parameters.TryGetValue(key, out raw);
            return CompositeIeStepHelper.ResolveTemplate(raw, _variableResolver, context, rowIndex);
        }
    }
}
