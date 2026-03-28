using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public class XmlPublishedWorkflowStore : IPublishedWorkflowStore
    {
        private readonly string _manifestPath;

        public XmlPublishedWorkflowStore()
        {
            PublishedDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Workflows", "Published");
            Directory.CreateDirectory(PublishedDirectory);
            _manifestPath = Path.Combine(PublishedDirectory, "published-workflows.xml");
        }

        public string PublishedDirectory { get; private set; }

        public async Task<IList<PublishedWorkflowRecord>> LoadAllAsync()
        {
            if (!File.Exists(_manifestPath))
            {
                return new List<PublishedWorkflowRecord>();
            }

            var document = XDocument.Load(_manifestPath);
            var result = document.Root != null
                ? document.Root.Elements("workflow").Select(DeserializeRecord).Where(item => item != null).ToList()
                : new List<PublishedWorkflowRecord>();
            return await Task.FromResult(result);
        }

        public async Task<PublishedWorkflowRecord> GetByWorkflowIdAsync(string workflowId)
        {
            var all = await LoadAllAsync();
            return all.FirstOrDefault(item => string.Equals(item.WorkflowId, workflowId, StringComparison.OrdinalIgnoreCase));
        }

        public async Task SaveAsync(PublishedWorkflowRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException("record");
            }

            var records = (await LoadAllAsync()).ToList();
            var existing = records.FirstOrDefault(item => string.Equals(item.WorkflowId, record.WorkflowId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                records.Remove(existing);
            }

            records.Add(record);
            SaveRecords(records);
        }

        public async Task RemoveAsync(string workflowId)
        {
            var records = (await LoadAllAsync())
                .Where(item => !string.Equals(item.WorkflowId, workflowId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            SaveRecords(records);
        }

        private void SaveRecords(IList<PublishedWorkflowRecord> records)
        {
            Directory.CreateDirectory(PublishedDirectory);
            var document = new XDocument(
                new XElement("publishedWorkflows",
                    records.Select(SerializeRecord)));
            document.Save(_manifestPath);
        }

        private static XElement SerializeRecord(PublishedWorkflowRecord record)
        {
            return new XElement("workflow",
                new XAttribute("workflowId", record.WorkflowId ?? string.Empty),
                new XAttribute("workflowName", record.WorkflowName ?? string.Empty),
                new XAttribute("workflowType", record.WorkflowType),
                new XAttribute("applicableRole", record.ApplicableRole ?? string.Empty),
                new XAttribute("sourcePath", record.SourcePath ?? string.Empty),
                new XAttribute("publishedSnapshotPath", record.PublishedSnapshotPath ?? string.Empty),
                new XAttribute("publishedAt", record.PublishedAt.ToString("o")),
                new XAttribute("version", record.Version ?? string.Empty));
        }

        private static PublishedWorkflowRecord DeserializeRecord(XElement element)
        {
            if (element == null)
            {
                return null;
            }

            DateTime publishedAt;
            WorkflowType workflowType;
            Enum.TryParse((string)element.Attribute("workflowType"), true, out workflowType);
            DateTime.TryParse((string)element.Attribute("publishedAt"), out publishedAt);

            return new PublishedWorkflowRecord
            {
                WorkflowId = (string)element.Attribute("workflowId"),
                WorkflowName = (string)element.Attribute("workflowName"),
                WorkflowType = workflowType,
                ApplicableRole = (string)element.Attribute("applicableRole"),
                SourcePath = (string)element.Attribute("sourcePath"),
                PublishedSnapshotPath = (string)element.Attribute("publishedSnapshotPath"),
                PublishedAt = publishedAt == default(DateTime) ? DateTime.MinValue : publishedAt,
                Version = (string)element.Attribute("version")
            };
        }
    }
}
