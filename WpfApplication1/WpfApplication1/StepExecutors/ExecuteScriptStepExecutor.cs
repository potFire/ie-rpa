using System;
using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class ExecuteScriptStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public ExecuteScriptStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.ExecuteScript; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = context.CurrentPage as IIePage;
            if (page == null)
            {
                throw new InvalidOperationException("当前没有可执行脚本的 IE 页面。");
            }

            string script;
            string resultExpression;
            string resultVariableName;
            step.Parameters.TryGetValue("script", out script);
            step.Parameters.TryGetValue("resultExpression", out resultExpression);
            step.Parameters.TryGetValue("resultVariableName", out resultVariableName);

            script = _variableResolver.ResolveString(script, context);
            resultExpression = _variableResolver.ResolveString(resultExpression, context);
            resultVariableName = _variableResolver.ResolveString(resultVariableName, context);

            if (string.IsNullOrWhiteSpace(script))
            {
                return Task.FromResult(StepExecutionResult.Failure("未配置 script 参数。"));
            }

            // 先执行主脚本，再按需读取脚本结果。
            // 这样既能兼容“只执行不取值”的场景，也能支持结果写回变量。
            page.ExecuteScript(script);

            if (!string.IsNullOrWhiteSpace(resultVariableName))
            {
                if (string.IsNullOrWhiteSpace(resultExpression))
                {
                    resultExpression = "window.__ieRpaResult";
                }

                var result = page.EvaluateScript(resultExpression);
                context.Variables[resultVariableName] = result ?? string.Empty;
                return Task.FromResult(StepExecutionResult.Success("脚本执行完成，结果已写入变量：" + resultVariableName));
            }

            return Task.FromResult(StepExecutionResult.Success("脚本执行完成。"));
        }
    }
}
