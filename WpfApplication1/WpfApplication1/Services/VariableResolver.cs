using System;
using WpfApplication1.Workflow;

namespace WpfApplication1.Services
{
    public class VariableResolver : IVariableResolver
    {
        public string ResolveString(string template, IExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(template) || context == null)
            {
                return template;
            }

            var result = template;
            foreach (var pair in context.Variables)
            {
                var token = "${" + pair.Key + "}";
                var value = pair.Value != null ? Convert.ToString(pair.Value) : string.Empty;
                result = result.Replace(token, value ?? string.Empty);
            }

            return result;
        }
    }
}
