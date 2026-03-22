using System;
using System.Threading.Tasks;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Selectors;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class ReadTextStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public ReadTextStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.ReadText; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = context.CurrentPage as IIePage;
            if (page == null)
            {
                throw new InvalidOperationException("当前没有可读取的 IE 页面。");
            }

            string selectorRaw;
            string variableName;
            step.Parameters.TryGetValue("selector", out selectorRaw);
            step.Parameters.TryGetValue("variableName", out variableName);

            var selector = SelectorParser.Parse(_variableResolver.ResolveString(selectorRaw, context));
            var element = page.FindElement(selector);
            var text = element.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = element.Value;
            }

            if (string.IsNullOrWhiteSpace(variableName))
            {
                variableName = "LastReadText";
            }

            context.Variables[variableName] = text ?? string.Empty;
            return Task.FromResult(StepExecutionResult.Success("已读取文本到变量：" + variableName));
        }
    }
}
