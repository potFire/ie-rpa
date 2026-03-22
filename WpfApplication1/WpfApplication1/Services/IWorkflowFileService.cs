using System.Threading.Tasks;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public interface IWorkflowFileService
    {
        Task SaveAsync(string path, WorkflowDefinition workflow);

        Task<WorkflowDefinition> LoadAsync(string path);
    }
}
