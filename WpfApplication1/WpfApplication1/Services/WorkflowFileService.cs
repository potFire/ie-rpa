using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public class WorkflowFileService : IWorkflowFileService
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public Task SaveAsync(string path, WorkflowDefinition workflow)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = _serializer.Serialize(workflow);
            File.WriteAllText(path, json, Encoding.UTF8);
            return Task.FromResult(0);
        }

        public Task<WorkflowDefinition> LoadAsync(string path)
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var workflow = _serializer.Deserialize<WorkflowDefinition>(json) ?? new WorkflowDefinition();
            NormalizeLegacyWorkflow(json, workflow);
            return Task.FromResult(workflow);
        }

        private static void NormalizeLegacyWorkflow(string json, WorkflowDefinition workflow)
        {
            if (workflow == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                workflow.EnsureCanvasLayout();
                return;
            }

            if (json.IndexOf("\"WorkflowType\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                workflow.WorkflowType = WorkflowType.General;
            }

            if (json.IndexOf("\"Description\"", StringComparison.OrdinalIgnoreCase) < 0 && workflow.Description == null)
            {
                workflow.Description = string.Empty;
            }

            if (json.IndexOf("\"ApplicableRole\"", StringComparison.OrdinalIgnoreCase) < 0 && workflow.ApplicableRole == null)
            {
                workflow.ApplicableRole = string.Empty;
            }

            workflow.EnsureCanvasLayout();
        }
    }
}
