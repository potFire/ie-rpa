using System.Collections.Generic;
using System.Threading.Tasks;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public interface IPublishedWorkflowStore
    {
        string PublishedDirectory { get; }

        Task<IList<PublishedWorkflowRecord>> LoadAllAsync();

        Task<PublishedWorkflowRecord> GetByWorkflowIdAsync(string workflowId);

        Task SaveAsync(PublishedWorkflowRecord record);

        Task RemoveAsync(string workflowId);
    }
}
