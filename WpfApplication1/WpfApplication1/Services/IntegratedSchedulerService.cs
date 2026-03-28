using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.StepExecutors;
using WpfApplication1.Workflow;

namespace WpfApplication1.Services
{
    public class IntegratedSchedulerService : IIntegratedSchedulerService
    {
        private readonly IWorkflowRunner _workflowRunner;
        private readonly IWorkflowFileService _workflowFileService;
        private readonly IBusinessStateStore _businessStateStore;
        private readonly ISchedulerStateStore _schedulerStateStore;
        private readonly IPublishedWorkflowStore _publishedWorkflowStore;
        private readonly ILogService _logService;
        private volatile bool _stopRequested;
        private LocalSchedulerState _currentState;

        public IntegratedSchedulerService(
            IWorkflowRunner workflowRunner,
            IWorkflowFileService workflowFileService,
            IBusinessStateStore businessStateStore,
            ISchedulerStateStore schedulerStateStore,
            IPublishedWorkflowStore publishedWorkflowStore,
            ILogService logService)
        {
            _workflowRunner = workflowRunner;
            _workflowFileService = workflowFileService;
            _businessStateStore = businessStateStore;
            _schedulerStateStore = schedulerStateStore;
            _publishedWorkflowStore = publishedWorkflowStore;
            _logService = logService;
            _currentState = new LocalSchedulerState();
        }

        public bool IsRunning { get; private set; }

        public LocalSchedulerState CurrentState
        {
            get { return _currentState; }
        }

        public async Task StartAsync(SchedulerSettings settings, SchedulerExecutionContext executionContext, CancellationToken cancellationToken)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException("executionContext");
            }

            settings.Normalize();
            _stopRequested = false;
            IsRunning = true;

            try
            {
                while (!_stopRequested && !cancellationToken.IsCancellationRequested)
                {
                    var result = await ExecuteSingleRoundCoreAsync(settings, executionContext, cancellationToken);
                    var delayMs = result == SchedulerRoundResult.IdleNoApply
                        ? settings.QueryIntervalWhenNoApplyMs
                        : settings.MainLoopIntervalMs;
                    await Task.Delay(Math.Max(200, delayMs), cancellationToken);
                }
            }
            finally
            {
                IsRunning = false;
                _stopRequested = false;
            }
        }

        public Task ExecuteSingleRoundAsync(SchedulerSettings settings, SchedulerExecutionContext executionContext, CancellationToken cancellationToken)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException("executionContext");
            }

            settings.Normalize();
            return ExecuteSingleRoundCoreAsync(settings, executionContext, cancellationToken);
        }

        public void RequestStop()
        {
            _stopRequested = true;
        }

        private async Task<SchedulerRoundResult> ExecuteSingleRoundCoreAsync(
            SchedulerSettings settings,
            SchedulerExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LoadStateIfNeededAsync(executionContext);

            if (executionContext.PendingBusinessState != null
                && !executionContext.PendingBusinessState.IsCompleted
                && settings.ResumePromptOnStartup)
            {
                await ExecuteResumeAsync(settings, executionContext, cancellationToken);
                return SchedulerRoundResult.Resume;
            }

            if (_currentState.ContinuousApplyCount >= Math.Max(1, settings.MaxContinuousApplyCount))
            {
                await ExecuteQueryAsync(settings, executionContext, cancellationToken, true);
                return SchedulerRoundResult.Query;
            }

            if (!string.IsNullOrWhiteSpace(settings.ApplyWorkflowId))
            {
                var applyProbe = await ExecuteApplyProbeAsync(settings, executionContext, cancellationToken);
                if (applyProbe == SchedulerRoundResult.Apply)
                {
                    return SchedulerRoundResult.Apply;
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.QueryWorkflowId))
            {
                await ExecuteQueryAsync(settings, executionContext, cancellationToken, false);
                return _currentState.HasPendingQuery ? SchedulerRoundResult.Query : SchedulerRoundResult.IdleNoApply;
            }

            return SchedulerRoundResult.IdleNoApply;
        }

        private async Task LoadStateIfNeededAsync(SchedulerExecutionContext executionContext)
        {
            if (_currentState != null && _currentState.LastRunAt.HasValue)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(executionContext.SchedulerStatePath) || !File.Exists(executionContext.SchedulerStatePath))
            {
                _currentState = _currentState ?? new LocalSchedulerState();
                PublishSummary(executionContext, executionContext.PendingBusinessState);
                return;
            }

            try
            {
                _currentState = await _schedulerStateStore.LoadAsync(executionContext.SchedulerStatePath) ?? new LocalSchedulerState();
            }
            catch
            {
                _currentState = new LocalSchedulerState();
            }

            PublishSummary(executionContext, executionContext.PendingBusinessState);
        }

        private async Task ExecuteResumeAsync(SchedulerSettings settings, SchedulerExecutionContext executionContext, CancellationToken cancellationToken)
        {
            var record = await ResolvePublishedRecordForResumeAsync(settings, executionContext.PendingBusinessState);
            if (record == null || string.IsNullOrWhiteSpace(record.PublishedSnapshotPath) || !File.Exists(record.PublishedSnapshotPath))
            {
                return;
            }

            var workflow = await _workflowFileService.LoadAsync(record.PublishedSnapshotPath);
            if (workflow == null || workflow.Steps == null || workflow.Steps.Count == 0)
            {
                return;
            }

            var startIndex = ResolveResumeStepIndex(workflow, executionContext.PendingBusinessState);
            if (startIndex < 0)
            {
                startIndex = 0;
            }

            _currentState.LastMode = "resume";
            await ExecuteWorkflowAsync(record.PublishedSnapshotPath, workflow, startIndex, null, "resume", executionContext, cancellationToken);
        }

        private async Task<SchedulerRoundResult> ExecuteApplyProbeAsync(
            SchedulerSettings settings,
            SchedulerExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            var record = await _publishedWorkflowStore.GetByWorkflowIdAsync(settings.ApplyWorkflowId);
            if (record == null || string.IsNullOrWhiteSpace(record.PublishedSnapshotPath) || !File.Exists(record.PublishedSnapshotPath))
            {
                return SchedulerRoundResult.IdleNoApply;
            }

            var workflow = await _workflowFileService.LoadAsync(record.PublishedSnapshotPath);
            if (workflow == null || workflow.Steps == null || workflow.Steps.Count == 0)
            {
                return SchedulerRoundResult.IdleNoApply;
            }

            if (workflow.Steps[0].StepType != StepType.HttpGetData)
            {
                _logService.Log(LogLevel.Warning, "Apply workflow first step is not HttpGetData. Current round skipped.");
                return SchedulerRoundResult.IdleNoApply;
            }

            _currentState.LastMode = "applyProbe";
            var probeResult = await ExecuteWorkflowAsync(record.PublishedSnapshotPath, workflow, 0, 1, "applyProbe", executionContext, cancellationToken);
            var firstStep = workflow.Steps[0];
            var hasDataVariableName = GetParameterOrDefault(firstStep, "hasDataVariableName", "HasApiData");
            var hasData = ReadBooleanVariable(probeResult.Context, hasDataVariableName, true);
            _currentState.HasPendingApply = hasData;

            if (!hasData)
            {
                _currentState.ContinuousApplyCount = 0;
                await PersistSchedulerStateAsync(executionContext);
                PublishSummary(executionContext, probeResult.Context.CurrentBusinessState);
                return SchedulerRoundResult.IdleNoApply;
            }

            _currentState.LastMode = "apply";
            _currentState.ContinuousApplyCount++;
            if (workflow.Steps.Count > 1)
            {
                await ExecuteWorkflowAsync(record.PublishedSnapshotPath, workflow, 1, null, "apply", executionContext, cancellationToken, probeResult.Context.CurrentBusinessState);
            }
            else
            {
                executionContext.PendingBusinessState = probeResult.Context.CurrentBusinessState != null ? probeResult.Context.CurrentBusinessState.Clone() : null;
                if (executionContext.BusinessStateChanged != null)
                {
                    executionContext.BusinessStateChanged(executionContext.PendingBusinessState != null ? executionContext.PendingBusinessState.Clone() : null);
                }
            }

            _currentState.HasPendingApply = executionContext.PendingBusinessState != null && !executionContext.PendingBusinessState.IsCompleted;
            await PersistSchedulerStateAsync(executionContext);
            PublishSummary(executionContext, executionContext.PendingBusinessState);
            return SchedulerRoundResult.Apply;
        }

        private async Task ExecuteQueryAsync(
            SchedulerSettings settings,
            SchedulerExecutionContext executionContext,
            CancellationToken cancellationToken,
            bool forcedByQuota)
        {
            var record = await _publishedWorkflowStore.GetByWorkflowIdAsync(settings.QueryWorkflowId);
            if (record == null || string.IsNullOrWhiteSpace(record.PublishedSnapshotPath) || !File.Exists(record.PublishedSnapshotPath))
            {
                return;
            }

            var workflow = await _workflowFileService.LoadAsync(record.PublishedSnapshotPath);
            if (workflow == null || workflow.Steps == null || workflow.Steps.Count == 0)
            {
                return;
            }

            _currentState.LastMode = forcedByQuota ? "queryForced" : "query";
            var result = await ExecuteWorkflowAsync(record.PublishedSnapshotPath, workflow, 0, null, forcedByQuota ? "queryForced" : "query", executionContext, cancellationToken);
            _currentState.HasPendingQuery = ReadPositiveIntVariable(result.Context, "LastPendingCount") > 0;
            _currentState.HasPendingUpload = executionContext.PendingBusinessState != null
                                             && executionContext.PendingBusinessState.Stage == BusinessStateStage.ReportSaved;
            _currentState.ContinuousApplyCount = 0;
            await PersistSchedulerStateAsync(executionContext);
            PublishSummary(executionContext, result.Context.CurrentBusinessState);
        }

        private async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
            string workflowPath,
            WorkflowDefinition workflow,
            int startStepIndex,
            int? maxSteps,
            string mode,
            SchedulerExecutionContext executionContext,
            CancellationToken cancellationToken,
            BusinessStateRecord seedBusinessState = null)
        {
            var startedAt = DateTime.Now;
            _logService.BeginRun(workflow, workflowPath, mode, string.Format("{0}-{1}", workflow != null ? workflow.Name : "workflow", mode));
            var context = new WpfApplication1.Workflow.ExecutionContext(cancellationToken)
            {
                BusinessStateStore = _businessStateStore,
                BusinessStatePath = executionContext.BusinessStatePath,
                CurrentBusinessState = seedBusinessState != null
                    ? seedBusinessState.Clone()
                    : (executionContext.PendingBusinessState != null ? executionContext.PendingBusinessState.Clone() : null),
                SchedulerMode = mode,
                CurrentWorkflowPath = workflowPath,
                WorkflowId = workflow != null ? workflow.Id : string.Empty,
                WorkflowName = workflow != null ? workflow.Name : Path.GetFileNameWithoutExtension(workflowPath),
                WorkflowType = workflow != null ? workflow.WorkflowType : WorkflowType.General,
                RunId = _logService.CurrentRun != null ? _logService.CurrentRun.RunId : string.Empty,
                RunName = _logService.CurrentRun != null ? _logService.CurrentRun.RunName : mode
            };

            context.Variables["EmployeeId"] = executionContext.EmployeeId ?? string.Empty;
            context.Variables["JobNo"] = executionContext.EmployeeId ?? string.Empty;
            context.Variables["BusinessStateFilePath"] = executionContext.BusinessStatePath ?? string.Empty;
            BusinessStateSupport.SyncVariables(context);
            context.RuntimeState.CurrentMode = mode;
            context.RuntimeState.CurrentWorkflowName = workflow != null ? workflow.Name : string.Empty;
            context.RuntimeState.CurrentWorkflowPath = workflowPath;
            context.RuntimeState.LastUpdatedAt = DateTime.Now;
            if (executionContext.RuntimeStateChanged != null)
            {
                context.RuntimeStateChanged += executionContext.RuntimeStateChanged;
                executionContext.RuntimeStateChanged(context.RuntimeState.Clone());
            }

            var history = new RunHistoryItem
            {
                WorkflowName = workflow != null ? workflow.Name : Path.GetFileNameWithoutExtension(workflowPath),
                Mode = mode,
                StartedAt = startedAt
            };
            var finalResult = "Success";
            var finalSummary = string.Empty;

            try
            {
                await _workflowRunner.RunAsync(workflow, context, startStepIndex, maxSteps);
                history.Result = "Success";
                finalSummary = string.Format("调度模式 {0} 执行完成。", mode);
            }
            catch (OperationCanceledException)
            {
                history.Result = "Cancelled";
                history.ErrorSummary = "Scheduler execution cancelled.";
                finalResult = "Cancelled";
                finalSummary = history.ErrorSummary;
                _logService.Log(LogLevel.Warning, history.ErrorSummary, null, null, context, "WorkflowCancelled");
                throw;
            }
            catch (Exception ex)
            {
                history.Result = "Failed";
                history.ErrorSummary = ex.Message;
                finalResult = "Failed";
                finalSummary = ex.Message;
                _currentState.LastError = ex.Message;
                if (context.RuntimeState != null)
                {
                    context.RuntimeState.RecentErrorSummary = ex.Message;
                    context.UpdateRuntimeState(state => state.RecentErrorSummary = ex.Message);
                }
                _logService.Log(LogLevel.Error, "Scheduler workflow failed: " + ex.Message, null, null, context, "WorkflowFailed", 0, 0, ex);
                throw;
            }
            finally
            {
                history.EndedAt = DateTime.Now;
                history.Duration = history.EndedAt.Value - history.StartedAt;
                _logService.EndRun(finalResult, finalSummary);
                if (executionContext.RunHistoryAdded != null)
                {
                    executionContext.RunHistoryAdded(history);
                }

                if (executionContext.RuntimeStateChanged != null)
                {
                    context.RuntimeStateChanged -= executionContext.RuntimeStateChanged;
                }
            }

            executionContext.PendingBusinessState = context.CurrentBusinessState != null ? context.CurrentBusinessState.Clone() : null;
            if (executionContext.BusinessStateChanged != null)
            {
                executionContext.BusinessStateChanged(executionContext.PendingBusinessState != null ? executionContext.PendingBusinessState.Clone() : null);
            }

            _currentState.LastWorkflowPath = workflowPath;
            _currentState.LastStepId = context.RuntimeState.CurrentStepId;
            _currentState.LastRunAt = DateTime.Now;
            _currentState.LastError = string.Empty;

            return new WorkflowExecutionResult
            {
                Workflow = workflow,
                Context = context
            };
        }

        private async Task PersistSchedulerStateAsync(SchedulerExecutionContext executionContext)
        {
            if (string.IsNullOrWhiteSpace(executionContext.SchedulerStatePath))
            {
                return;
            }

            await _schedulerStateStore.SaveAsync(executionContext.SchedulerStatePath, _currentState);
        }

        private void PublishSummary(SchedulerExecutionContext executionContext, BusinessStateRecord businessState)
        {
            var summary = new TaskSummarySnapshot
            {
                PendingApplyCount = _currentState.HasPendingApply ? 1 : 0,
                PendingApprovalCount = businessState != null && businessState.Stage == BusinessStateStage.PendingApproval ? 1 : 0,
                PendingQueryCount = _currentState.HasPendingQuery ? 1 : 0,
                PendingResumeCount = businessState != null && !businessState.IsCompleted ? 1 : 0,
                HasPendingTask = _currentState.HasPendingApply || _currentState.HasPendingQuery || (businessState != null && !businessState.IsCompleted),
                SuggestedAction = ResolveSuggestedAction(businessState),
                LastRefreshAt = DateTime.Now
            };

            if (executionContext.TaskSummaryChanged != null)
            {
                executionContext.TaskSummaryChanged(summary);
            }
        }

        private static string ResolveSuggestedAction(BusinessStateRecord businessState)
        {
            if (businessState == null)
            {
                return "Continue monitoring integrated scheduling.";
            }

            switch (businessState.Stage)
            {
                case BusinessStateStage.PendingApproval:
                    return "Review the approval workflow or open the approval queue.";
                case BusinessStateStage.Queryable:
                case BusinessStateStage.ReportSaved:
                    return "Open the query flow and export the report.";
                case BusinessStateStage.Failed:
                    return "Resume the failed task from local tasks.";
                default:
                    return "Continue integrated scheduling.";
            }
        }

        private static int ResolveResumeStepIndex(WorkflowDefinition workflow, BusinessStateRecord state)
        {
            if (workflow == null || workflow.Steps == null || state == null)
            {
                return -1;
            }

            var stageToken = state.Stage.ToString();
            for (var index = 0; index < workflow.Steps.Count; index++)
            {
                var step = workflow.Steps[index];
                if (step == null || step.Parameters == null)
                {
                    continue;
                }

                string resumeStages;
                if (!step.Parameters.TryGetValue("resumeStages", out resumeStages)
                    && !step.Parameters.TryGetValue("resumeStage", out resumeStages))
                {
                    continue;
                }

                var tokens = (resumeStages ?? string.Empty)
                    .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim());
                if (tokens.Any(item => string.Equals(item, stageToken, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item, "Any", StringComparison.OrdinalIgnoreCase)))
                {
                    return index;
                }
            }

            return -1;
        }

        private async Task<PublishedWorkflowRecord> ResolvePublishedRecordForResumeAsync(SchedulerSettings settings, BusinessStateRecord state)
        {
            if (state == null)
            {
                return !string.IsNullOrWhiteSpace(_currentState.LastWorkflowPath)
                    ? new PublishedWorkflowRecord { PublishedSnapshotPath = _currentState.LastWorkflowPath }
                    : null;
            }

            switch (state.Stage)
            {
                case BusinessStateStage.PendingApproval:
                case BusinessStateStage.Approved:
                    return !string.IsNullOrWhiteSpace(settings.ApprovalWorkflowId)
                        ? await _publishedWorkflowStore.GetByWorkflowIdAsync(settings.ApprovalWorkflowId)
                        : (!string.IsNullOrWhiteSpace(_currentState.LastWorkflowPath)
                            ? new PublishedWorkflowRecord { PublishedSnapshotPath = _currentState.LastWorkflowPath }
                            : null);
                case BusinessStateStage.Queryable:
                case BusinessStateStage.ReportSaved:
                case BusinessStateStage.Uploaded:
                    return !string.IsNullOrWhiteSpace(settings.QueryWorkflowId)
                        ? await _publishedWorkflowStore.GetByWorkflowIdAsync(settings.QueryWorkflowId)
                        : (!string.IsNullOrWhiteSpace(_currentState.LastWorkflowPath)
                            ? new PublishedWorkflowRecord { PublishedSnapshotPath = _currentState.LastWorkflowPath }
                            : null);
                case BusinessStateStage.Failed:
                    if (!string.IsNullOrWhiteSpace(_currentState.LastWorkflowPath))
                    {
                        return new PublishedWorkflowRecord { PublishedSnapshotPath = _currentState.LastWorkflowPath };
                    }

                    return !string.IsNullOrWhiteSpace(settings.ApplyWorkflowId)
                        ? await _publishedWorkflowStore.GetByWorkflowIdAsync(settings.ApplyWorkflowId)
                        : null;
                default:
                    return !string.IsNullOrWhiteSpace(settings.ApplyWorkflowId)
                        ? await _publishedWorkflowStore.GetByWorkflowIdAsync(settings.ApplyWorkflowId)
                        : (!string.IsNullOrWhiteSpace(_currentState.LastWorkflowPath)
                            ? new PublishedWorkflowRecord { PublishedSnapshotPath = _currentState.LastWorkflowPath }
                            : null);
            }
        }

        private static string GetParameterOrDefault(WorkflowStep step, string key, string defaultValue)
        {
            if (step == null || step.Parameters == null)
            {
                return defaultValue;
            }

            string value;
            return step.Parameters.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : defaultValue;
        }

        private static bool ReadBooleanVariable(IExecutionContext context, string key, bool defaultValue)
        {
            if (context == null || context.Variables == null || string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            object value;
            if (!context.Variables.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            bool result;
            return bool.TryParse(Convert.ToString(value), out result) ? result : defaultValue;
        }

        private static int ReadPositiveIntVariable(IExecutionContext context, string key)
        {
            if (context == null || context.Variables == null || string.IsNullOrWhiteSpace(key))
            {
                return 0;
            }

            object value;
            if (!context.Variables.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }

            int result;
            return int.TryParse(Convert.ToString(value), out result) ? Math.Max(0, result) : 0;
        }

        private enum SchedulerRoundResult
        {
            IdleNoApply,
            Apply,
            Query,
            Resume
        }

        private class WorkflowExecutionResult
        {
            public WorkflowDefinition Workflow { get; set; }

            public WpfApplication1.Workflow.ExecutionContext Context { get; set; }
        }
    }
}

