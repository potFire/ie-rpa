using System;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class UpdateBusinessStateStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public UpdateBusinessStateStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.UpdateBusinessState; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var state = BusinessStateSupport.EnsureRecord(context);
            ApplyValue(step, context, "name", value => state.Name = value);
            ApplyValue(step, context, "idCardNumber", value => state.IdCardNumber = value);
            ApplyValue(step, context, "reason", value => state.Reason = value);
            ApplyValue(step, context, "sourceUrl", value => state.SourceUrl = value);
            ApplyValue(step, context, "machineRole", value => state.MachineRole = value);
            ApplyValue(step, context, "lastStepName", value => state.LastStepName = value);
            ApplyValue(step, context, "errorMessage", value => state.ErrorMessage = value);
            ApplyValue(step, context, "htmlFilePath", value => state.HtmlFilePath = value);
            ApplyValue(step, context, "uploadResult", value => state.UploadResult = value);

            string stageRaw;
            if (step.Parameters.TryGetValue("stage", out stageRaw))
            {
                stageRaw = _variableResolver.ResolveString(stageRaw, context);
                if (!string.IsNullOrWhiteSpace(stageRaw))
                {
                    state.Stage = BusinessStateSupport.ResolveStage(stageRaw);
                }
            }

            string fetchedAtRaw;
            if (step.Parameters.TryGetValue("fetchedAt", out fetchedAtRaw))
            {
                fetchedAtRaw = _variableResolver.ResolveString(fetchedAtRaw, context);
                state.FetchedAt = string.IsNullOrWhiteSpace(fetchedAtRaw) ? DateTime.Now : BusinessStateSupport.ResolveDateTime(fetchedAtRaw) ?? DateTime.Now;
            }
            else if (state.Stage == BusinessStateStage.Fetched && !state.FetchedAt.HasValue)
            {
                state.FetchedAt = DateTime.Now;
            }

            string uploadedAtRaw;
            if (step.Parameters.TryGetValue("uploadedAt", out uploadedAtRaw))
            {
                uploadedAtRaw = _variableResolver.ResolveString(uploadedAtRaw, context);
                state.UploadedAt = string.IsNullOrWhiteSpace(uploadedAtRaw) ? DateTime.Now : BusinessStateSupport.ResolveDateTime(uploadedAtRaw) ?? DateTime.Now;
            }
            else if (state.Stage == BusinessStateStage.Uploaded && !state.UploadedAt.HasValue)
            {
                state.UploadedAt = DateTime.Now;
            }

            string retryDeltaRaw;
            if (step.Parameters.TryGetValue("retryCountDelta", out retryDeltaRaw))
            {
                retryDeltaRaw = _variableResolver.ResolveString(retryDeltaRaw, context);
                int retryDelta;
                if (int.TryParse(retryDeltaRaw, out retryDelta))
                {
                    state.RetryCount = Math.Max(0, state.RetryCount + retryDelta);
                }
            }

            string completedRaw;
            if (step.Parameters.TryGetValue("markCompleted", out completedRaw))
            {
                completedRaw = _variableResolver.ResolveString(completedRaw, context);
                state.IsCompleted = CompositeIeStepHelper.ResolveBoolean(completedRaw, state.IsCompleted);
            }

            state.LastUpdatedAt = DateTime.Now;
            BusinessStateSupport.SyncVariables(context);
            await BusinessStateSupport.PersistAsync(context);
            return StepExecutionResult.Success("业务状态已更新为：" + state.Stage);
        }

        private void ApplyValue(WorkflowStep step, IExecutionContext context, string key, Action<string> apply)
        {
            string raw;
            if (!step.Parameters.TryGetValue(key, out raw))
            {
                return;
            }

            raw = _variableResolver.ResolveString(raw, context);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                apply(raw);
            }
        }
    }
}