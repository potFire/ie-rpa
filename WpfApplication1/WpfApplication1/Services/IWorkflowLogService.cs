using System.Collections.Generic;
using System.Threading.Tasks;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public interface IWorkflowLogService
    {
        Task<IList<WorkflowLogWorkflowItem>> GetLoggedWorkflowsAsync();

        Task<IList<WorkflowLogRunItem>> GetRunsAsync(string workflowId);

        Task<string> LoadRunTextAsync(WorkflowLogRunItem runItem);

        Task ExportRunAsync(WorkflowLogRunItem runItem, string exportPath);
    }
}
