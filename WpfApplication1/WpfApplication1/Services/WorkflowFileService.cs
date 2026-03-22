using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
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
            var workflow = _serializer.Deserialize<WorkflowDefinition>(json);
            return Task.FromResult(workflow);
        }
    }
}
