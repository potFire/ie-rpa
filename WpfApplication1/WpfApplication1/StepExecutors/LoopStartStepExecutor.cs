using System;
using System.Linq;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class LoopStartStepExecutor : IStepExecutor
    {
        public StepType StepType
        {
            get { return StepType.LoopStart; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string loopKey;
            string requiredVariables;
            string iterationVariable;
            step.Parameters.TryGetValue("loopKey", out loopKey);
            step.Parameters.TryGetValue("requiredVariables", out requiredVariables);
            step.Parameters.TryGetValue("iterationVariable", out iterationVariable);

            if (string.IsNullOrWhiteSpace(loopKey))
            {
                return Task.FromResult(StepExecutionResult.Failure("未配置 loopKey。"));
            }

            LoopPairInfo loopPair;
            if (!context.LoopPairs.TryGetValue(loopKey, out loopPair))
            {
                return Task.FromResult(StepExecutionResult.Failure("未找到对应的结束循环：" + loopKey));
            }

            LoopRuntimeState runtimeState;
            if (!context.LoopStates.TryGetValue(loopKey, out runtimeState))
            {
                runtimeState = new LoopRuntimeState();
                context.LoopStates[loopKey] = runtimeState;
            }

            var missingVariables = SplitVariables(requiredVariables)
                .Where(variableName => !HasValue(context, variableName))
                .ToArray();
            if (missingVariables.Length > 0)
            {
                runtimeState.SkipCurrentCycle = true;
                var skipResult = StepExecutionResult.Success("循环条件未满足，跳过本轮：" + string.Join(", ", missingVariables));
                skipResult.NextStepIndex = loopPair.EndStepIndex;
                return Task.FromResult(skipResult);
            }

            runtimeState.SkipCurrentCycle = false;
            if (!string.IsNullOrWhiteSpace(iterationVariable))
            {
                context.Variables[iterationVariable] = runtimeState.NextIterationNumber;
            }

            return Task.FromResult(StepExecutionResult.Success("进入循环第 " + runtimeState.NextIterationNumber + " 轮。"));
        }

        private static string[] SplitVariables(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new string[0];
            }

            return raw
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool HasValue(IExecutionContext context, string variableName)
        {
            object value;
            if (!context.Variables.TryGetValue(variableName, out value) || value == null)
            {
                return false;
            }

            var text = value as string;
            return text == null || !string.IsNullOrWhiteSpace(text);
        }
    }
}
