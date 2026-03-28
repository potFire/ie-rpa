using System.Collections.Generic;
using System.Threading.Tasks;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public interface IWorkflowCatalogService
    {
        string WorkflowRootDirectory { get; }

        string CatalogDirectory { get; }

        Task<IList<WorkflowListItem>> GetWorkflowsAsync();

        Task<string> SaveWorkflowAsync(string path, WorkflowDefinition workflow);

        Task<string> CreateWorkflowAsync(WorkflowCreateRequest request, WorkflowDefinition workflow);

        Task<string> ImportWorkflowAsync(string importPath);

        Task ExportWorkflowAsync(string sourcePath, string exportPath);

        Task<string> DuplicateWorkflowAsync(string sourcePath);

        Task DeleteWorkflowAsync(string sourcePath);
    }
}
