using System;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class LoopStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public LoopStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.Loop; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string loopKey;
            string repeatFromStepIndexRaw;
            string timesRaw;
            string currentIterationVariable;

            step.Parameters.TryGetValue("loopKey", out loopKey);
            step.Parameters.TryGetValue("repeatFromStepIndex", out repeatFromStepIndexRaw);
            step.Parameters.TryGetValue("times", out timesRaw);
            step.Parameters.TryGetValue("currentIterationVariable", out currentIterationVariable);

            loopKey = _variableResolver.ResolveString(loopKey, context);
            currentIterationVariable = _variableResolver.ResolveString(currentIterationVariable, context);

            if (string.IsNullOrWhiteSpace(loopKey))
            {
                loopKey = step.Id;
            }

            int repeatFromStepIndex;
            if (!int.TryParse(repeatFromStepIndexRaw, out repeatFromStepIndex))
            {
                return Task.FromResult(StepExecutionResult.Failure("未配置 repeatFromStepIndex。"));
            }

            int times;
            if (!int.TryParse(timesRaw, out times) || times <= 1)
            {
                return Task.FromResult(StepExecutionResult.Success("循环次数小于等于 1，不执行回跳。"));
            }

            var internalKey = "__loop." + loopKey;
            var currentIteration = 0;
            if (context.Variables.ContainsKey(internalKey))
            {
                currentIteration = Convert.ToInt32(context.Variables[internalKey]);
            }

            // 这里约定 Loop 步骤放在循环体的尾部。
            // 例如第 0 到 2 步是循环体，第 3 步是 Loop，repeatFromStepIndex 填 0。
            if (currentIteration + 1 < times)
            {
                currentIteration++;
                context.Variables[internalKey] = currentIteration;
                if (!string.IsNullOrWhiteSpace(currentIterationVariable))
                {
                    context.Variables[currentIterationVariable] = currentIteration + 1;
                }

                var result = StepExecutionResult.Success("循环回跳，第 " + (currentIteration + 1) + " 次执行。" );
                result.NextStepIndex = repeatFromStepIndex;
                return Task.FromResult(result);
            }

            context.Variables.Remove(internalKey);
            if (!string.IsNullOrWhiteSpace(currentIterationVariable))
            {
                context.Variables[currentIterationVariable] = times;
            }

            return Task.FromResult(StepExecutionResult.Success("循环结束，共执行 " + times + " 次。"));
        }
    }
}
