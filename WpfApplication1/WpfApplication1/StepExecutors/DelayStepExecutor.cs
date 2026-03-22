using System;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class DelayStepExecutor : IStepExecutor
    {
        public StepType StepType
        {
            get { return StepType.Delay; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string rawDuration;
            var durationMs = 1000;

            if (step.Parameters.TryGetValue("durationMs", out rawDuration))
            {
                int parsedDuration;
                if (int.TryParse(rawDuration, out parsedDuration) && parsedDuration >= 0)
                {
                    durationMs = parsedDuration;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(durationMs), context.CancellationToken);
            return StepExecutionResult.Success("等待完成，耗时 " + durationMs + " ms。");
        }
    }
}
