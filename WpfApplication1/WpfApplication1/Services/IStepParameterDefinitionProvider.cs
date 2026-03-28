using System.Collections.Generic;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public interface IStepParameterDefinitionProvider
    {
        IReadOnlyList<StepParameterDefinition> GetDefinitions(StepType stepType);
    }
}
