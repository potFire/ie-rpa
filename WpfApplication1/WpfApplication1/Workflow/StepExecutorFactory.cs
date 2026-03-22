using System;
using System.Collections.Generic;
using System.Linq;
using WpfApplication1.Enums;

namespace WpfApplication1.Workflow
{
    public class StepExecutorFactory
    {
        private readonly IDictionary<StepType, IStepExecutor> _executors;

        public StepExecutorFactory(IEnumerable<IStepExecutor> executors)
        {
            _executors = executors.ToDictionary(item => item.StepType);
        }

        public IStepExecutor GetExecutor(StepType stepType)
        {
            if (!_executors.ContainsKey(stepType))
            {
                throw new InvalidOperationException("No executor registered for step type " + stepType + ".");
            }

            return _executors[stepType];
        }
    }
}
