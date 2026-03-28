using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public interface IWorkflowTemplateFactory
    {
        WorkflowDefinition CreateWorkflow(WorkflowCreateRequest request);
    }
}
