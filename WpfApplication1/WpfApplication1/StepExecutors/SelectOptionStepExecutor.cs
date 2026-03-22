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
    public class SelectOptionStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public SelectOptionStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.SelectOption; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = context.CurrentPage as IIePage;
            if (page == null)
            {
                throw new InvalidOperationException("当前没有可操作的 IE 页面。");
            }

            string selectorRaw;
            string optionValue;
            string matchMode;
            step.Parameters.TryGetValue("selector", out selectorRaw);
            step.Parameters.TryGetValue("option", out optionValue);
            step.Parameters.TryGetValue("matchMode", out matchMode);

            selectorRaw = _variableResolver.ResolveString(selectorRaw, context);
            optionValue = _variableResolver.ResolveString(optionValue, context);
            matchMode = _variableResolver.ResolveString(matchMode, context);

            var selector = SelectorParser.Parse(selectorRaw);
            var element = page.FindElement(selector);
            var byText = !string.Equals(matchMode, "value", StringComparison.OrdinalIgnoreCase);
            element.SelectOption(optionValue, byText);
            return Task.FromResult(StepExecutionResult.Success("下拉项已选择：" + optionValue));
        }
    }
}
