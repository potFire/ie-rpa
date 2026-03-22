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
    public class ClickElementStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public ClickElementStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.ClickElement; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = context.CurrentPage as IIePage;
            if (page == null)
            {
                throw new InvalidOperationException("当前没有可点击的 IE 页面。");
            }

            string selectorRaw;
            step.Parameters.TryGetValue("selector", out selectorRaw);
            var selector = SelectorParser.Parse(_variableResolver.ResolveString(selectorRaw, context));
            var element = page.FindElement(selector);
            element.Click();
            return Task.FromResult(StepExecutionResult.Success("已点击目标元素。"));
        }
    }
}
