using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class SetVariableStepExecutor : IStepExecutor
    {
        public StepType StepType
        {
            get { return StepType.SetVariable; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string name;
            if (!step.Parameters.TryGetValue("name", out name) || string.IsNullOrWhiteSpace(name))
            {
                return Task.FromResult(StepExecutionResult.Failure("未配置变量名。"));
            }

            string value;
            step.Parameters.TryGetValue("value", out value);
            context.Variables[name] = value ?? string.Empty;

            return Task.FromResult(StepExecutionResult.Success("变量已写入：" + name));
        }
    }
}
