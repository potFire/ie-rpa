using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class WriteLogStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public WriteLogStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.WriteLog; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string message;
            step.Parameters.TryGetValue("message", out message);
            message = _variableResolver.ResolveString(message, context);
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "日志步骤已执行。";
            }

            // 写日志步骤本身不直接落日志，而是把内容作为成功消息返回给运行器统一记录。
            // 这样日志格式、时间戳和失败截图能力都还能复用运行器现有的通道。
            return Task.FromResult(StepExecutionResult.Success(message));
        }
    }
}
