using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public class XmlSchedulerStateStore : ISchedulerStateStore
    {
        public Task SaveAsync(string path, LocalSchedulerState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var document = new XDocument(
                new XElement("schedulerState",
                    new XElement("lastMode", state.LastMode ?? string.Empty),
                    new XElement("continuousApplyCount", state.ContinuousApplyCount),
                    new XElement("hasPendingApply", state.HasPendingApply),
                    new XElement("hasPendingQuery", state.HasPendingQuery),
                    new XElement("hasPendingUpload", state.HasPendingUpload),
                    new XElement("lastWorkflowPath", state.LastWorkflowPath ?? string.Empty),
                    new XElement("lastStepId", state.LastStepId ?? string.Empty),
                    new XElement("lastRunAt", FormatDateTime(state.LastRunAt)),
                    new XElement("lastError", state.LastError ?? string.Empty)));

            document.Save(path);
            return Task.FromResult(0);
        }

        public Task<LocalSchedulerState> LoadAsync(string path)
        {
            var document = XDocument.Load(path);
            var root = document.Element("schedulerState");
            var state = new LocalSchedulerState
            {
                LastMode = GetElementValue(root, "lastMode"),
                ContinuousApplyCount = ParseInt(GetElementValue(root, "continuousApplyCount"), 0),
                HasPendingApply = ParseBool(GetElementValue(root, "hasPendingApply"), false),
                HasPendingQuery = ParseBool(GetElementValue(root, "hasPendingQuery"), false),
                HasPendingUpload = ParseBool(GetElementValue(root, "hasPendingUpload"), false),
                LastWorkflowPath = GetElementValue(root, "lastWorkflowPath"),
                LastStepId = GetElementValue(root, "lastStepId"),
                LastRunAt = ParseDateTime(GetElementValue(root, "lastRunAt")),
                LastError = GetElementValue(root, "lastError")
            };

            return Task.FromResult(state);
        }

        private static string GetElementValue(XElement parent, string name)
        {
            var element = parent != null ? parent.Element(name) : null;
            return element != null ? element.Value : string.Empty;
        }

        private static int ParseInt(string raw, int defaultValue)
        {
            int value;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : defaultValue;
        }

        private static bool ParseBool(string raw, bool defaultValue)
        {
            bool value;
            return bool.TryParse(raw, out value) ? value : defaultValue;
        }

        private static DateTime? ParseDateTime(string raw)
        {
            DateTime value;
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value)
                ? value
                : (DateTime?)null;
        }

        private static string FormatDateTime(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("o", CultureInfo.InvariantCulture) : string.Empty;
        }
    }
}
