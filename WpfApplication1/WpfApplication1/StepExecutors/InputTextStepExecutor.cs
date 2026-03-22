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
    public class InputTextStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public InputTextStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.InputText; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = context.CurrentPage as IIePage;
            if (page == null)
            {
                throw new InvalidOperationException("当前没有可输入的 IE 页面。");
            }

            string selectorRaw;
            string text;
            step.Parameters.TryGetValue("selector", out selectorRaw);
            step.Parameters.TryGetValue("text", out text);

            var selector = SelectorParser.Parse(_variableResolver.ResolveString(selectorRaw, context));
            var value = _variableResolver.ResolveString(text, context);
            var element = page.FindElement(selector);
            element.SetValue(value);
            return Task.FromResult(StepExecutionResult.Success("已输入文本。"));
        }
    }
}
