using System;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class LoopEndStepExecutor : IStepExecutor
    {
        public StepType StepType
        {
            get { return StepType.LoopEnd; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string loopKey;
            string mode;
            string timesRaw;
            string intervalMsRaw;
            step.Parameters.TryGetValue("loopKey", out loopKey);
            step.Parameters.TryGetValue("mode", out mode);
            step.Parameters.TryGetValue("times", out timesRaw);
            step.Parameters.TryGetValue("intervalMs", out intervalMsRaw);

            if (string.IsNullOrWhiteSpace(loopKey))
            {
                return StepExecutionResult.Failure("未配置 loopKey。");
            }

            LoopPairInfo loopPair;
            if (!context.LoopPairs.TryGetValue(loopKey, out loopPair))
            {
                return StepExecutionResult.Failure("未找到对应的开始循环：" + loopKey);
            }

            LoopRuntimeState runtimeState;
            if (!context.LoopStates.TryGetValue(loopKey, out runtimeState))
            {
                runtimeState = new LoopRuntimeState();
                context.LoopStates[loopKey] = runtimeState;
            }

            var intervalMs = ParseNonNegative(intervalMsRaw, 0);
            if (runtimeState.SkipCurrentCycle)
            {
                runtimeState.SkipCurrentCycle = false;
                await DelayIfNeeded(intervalMs, context);
                var skipped = StepExecutionResult.Success("已跳过当前循环，等待下一轮。");
                skipped.NextStepIndex = loopPair.StartStepIndex;
                return skipped;
            }

            var normalizedMode = string.IsNullOrWhiteSpace(mode) ? "infinite" : mode.Trim().ToLowerInvariant();
            runtimeState.CompletedIterations++;

            if (string.Equals(normalizedMode, "counted", StringComparison.OrdinalIgnoreCase))
            {
                var times = ParseNonNegative(timesRaw, 0);
                if (times <= 0)
                {
                    return StepExecutionResult.Failure("计次循环需要配置大于 0 的 times。");
                }

                if (runtimeState.CompletedIterations >= times)
                {
                    context.LoopStates.Remove(loopKey);
                    return StepExecutionResult.Success("循环结束，共执行 " + runtimeState.CompletedIterations + " 次。");
                }
            }

            runtimeState.NextIterationNumber = runtimeState.CompletedIterations + 1;
            await DelayIfNeeded(intervalMs, context);

            var result = StepExecutionResult.Success("循环等待完成，准备进入第 " + runtimeState.NextIterationNumber + " 轮。");
            result.NextStepIndex = loopPair.StartStepIndex;
            return result;
        }

        private static async Task DelayIfNeeded(int intervalMs, IExecutionContext context)
        {
            if (intervalMs > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(intervalMs), context.CancellationToken);
            }
        }

        private static int ParseNonNegative(string raw, int defaultValue)
        {
            int value;
            return int.TryParse(raw, out value) && value >= 0 ? value : defaultValue;
        }
    }
}
