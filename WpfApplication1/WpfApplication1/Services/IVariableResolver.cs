using WpfApplication1.Workflow;

namespace WpfApplication1.Services
{
    public interface IVariableResolver
    {
        string ResolveString(string template, IExecutionContext context);
    }
}
