using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public class XmlBusinessStateStore : IBusinessStateStore
    {
        public Task SaveAsync(string path, BusinessStateRecord state)
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
                new XElement("businessState",
                    new XElement("name", state.Name ?? string.Empty),
                    new XElement("idCardNumber", state.IdCardNumber ?? string.Empty),
                    new XElement("reason", state.Reason ?? string.Empty),
                    new XElement("sourceUrl", state.SourceUrl ?? string.Empty),
                    new XElement("fetchedAt", FormatDateTime(state.FetchedAt)),
                    new XElement("stage", state.Stage),
                    new XElement("machineRole", state.MachineRole ?? string.Empty),
                    new XElement("lastStepName", state.LastStepName ?? string.Empty),
                    new XElement("lastUpdatedAt", FormatDateTime(state.LastUpdatedAt)),
                    new XElement("errorMessage", state.ErrorMessage ?? string.Empty),
                    new XElement("retryCount", state.RetryCount),
                    new XElement("htmlFilePath", state.HtmlFilePath ?? string.Empty),
                    new XElement("uploadResult", state.UploadResult ?? string.Empty),
                    new XElement("uploadedAt", FormatDateTime(state.UploadedAt)),
                    new XElement("isCompleted", state.IsCompleted)));

            document.Save(path);
            return Task.FromResult(0);
        }

        public Task<BusinessStateRecord> LoadAsync(string path)
        {
            var document = XDocument.Load(path);
            var root = document.Element("businessState");
            var state = new BusinessStateRecord
            {
                Name = GetElementValue(root, "name"),
                IdCardNumber = GetElementValue(root, "idCardNumber"),
                Reason = GetElementValue(root, "reason"),
                SourceUrl = GetElementValue(root, "sourceUrl"),
                FetchedAt = ParseDateTime(GetElementValue(root, "fetchedAt")),
                Stage = ParseEnum(GetElementValue(root, "stage"), BusinessStateStage.None),
                MachineRole = GetElementValue(root, "machineRole"),
                LastStepName = GetElementValue(root, "lastStepName"),
                LastUpdatedAt = ParseDateTime(GetElementValue(root, "lastUpdatedAt")),
                ErrorMessage = GetElementValue(root, "errorMessage"),
                RetryCount = ParseInt(GetElementValue(root, "retryCount"), 0),
                HtmlFilePath = GetElementValue(root, "htmlFilePath"),
                UploadResult = GetElementValue(root, "uploadResult"),
                UploadedAt = ParseDateTime(GetElementValue(root, "uploadedAt")),
                IsCompleted = ParseBool(GetElementValue(root, "isCompleted"), false)
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
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : defaultValue;
        }

        private static bool ParseBool(string raw, bool defaultValue)
        {
            bool value;
            return bool.TryParse(raw, out value) ? value : defaultValue;
        }

        private static TEnum ParseEnum<TEnum>(string raw, TEnum defaultValue) where TEnum : struct
        {
            TEnum value;
            return Enum.TryParse(raw, true, out value) ? value : defaultValue;
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