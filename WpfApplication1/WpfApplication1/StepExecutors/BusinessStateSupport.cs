using System;
using System.Globalization;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    internal static class BusinessStateSupport
    {
        public static BusinessStateRecord EnsureRecord(IExecutionContext context)
        {
            if (context.CurrentBusinessState == null)
            {
                context.CurrentBusinessState = new BusinessStateRecord();
            }

            return context.CurrentBusinessState;
        }

        public static async Task PersistAsync(IExecutionContext context)
        {
            if (context == null || context.BusinessStateStore == null || string.IsNullOrWhiteSpace(context.BusinessStatePath) || context.CurrentBusinessState == null)
            {
                return;
            }

            context.CurrentBusinessState.LastUpdatedAt = DateTime.Now;
            await context.BusinessStateStore.SaveAsync(context.BusinessStatePath, context.CurrentBusinessState);
        }

        public static void SyncVariables(IExecutionContext context)
        {
            if (context == null || context.CurrentBusinessState == null)
            {
                return;
            }

            var state = context.CurrentBusinessState;
            context.Variables["BusinessState.Stage"] = state.Stage.ToString();
            context.Variables["BusinessState.Name"] = state.Name ?? string.Empty;
            context.Variables["BusinessState.IdCardNumber"] = state.IdCardNumber ?? string.Empty;
            context.Variables["BusinessState.Reason"] = state.Reason ?? string.Empty;
            context.Variables["BusinessState.SourceUrl"] = state.SourceUrl ?? string.Empty;
            context.Variables["BusinessState.HtmlFilePath"] = state.HtmlFilePath ?? string.Empty;
            context.Variables["BusinessState.UploadResult"] = state.UploadResult ?? string.Empty;
            context.Variables["BusinessState.IsCompleted"] = state.IsCompleted;
            context.Variables["BusinessName"] = state.Name ?? string.Empty;
            context.Variables["BusinessIdCardNumber"] = state.IdCardNumber ?? string.Empty;
            context.Variables["BusinessReason"] = state.Reason ?? string.Empty;
            context.Variables["BusinessHtmlFilePath"] = state.HtmlFilePath ?? string.Empty;
        }

        public static BusinessStateStage ResolveStage(string raw)
        {
            BusinessStateStage stage;
            return Enum.TryParse(raw, true, out stage) ? stage : BusinessStateStage.None;
        }

        public static DateTime? ResolveDateTime(string raw)
        {
            DateTime value;
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value)
                ? value
                : (DateTime?)null;
        }
    }
}