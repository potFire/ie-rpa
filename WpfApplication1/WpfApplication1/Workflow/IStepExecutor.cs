using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1.Workflow
{
    public interface IStepExecutor
    {
        StepType StepType { get; }

        Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context);
    }
}
