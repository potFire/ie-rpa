using System.Threading.Tasks;
using WpfApplication1.Models;

namespace WpfApplication1.Workflow
{
    public interface IWorkflowRunner
    {
        Task RunAsync(WorkflowDefinition workflow, IExecutionContext context, int startStepIndex = 0, int? maxSteps = null);
    }
}
