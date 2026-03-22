using System;
using System.Globalization;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class ConditionStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public ConditionStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.Condition; }
        }

        public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string left;
            string op;
            string right;
            string resultVariableName;
            string whenTrueStepIndexRaw;
            string whenFalseStepIndexRaw;

            step.Parameters.TryGetValue("left", out left);
            step.Parameters.TryGetValue("operator", out op);
            step.Parameters.TryGetValue("right", out right);
            step.Parameters.TryGetValue("resultVariableName", out resultVariableName);
            step.Parameters.TryGetValue("whenTrueStepIndex", out whenTrueStepIndexRaw);
            step.Parameters.TryGetValue("whenFalseStepIndex", out whenFalseStepIndexRaw);

            left = _variableResolver.ResolveString(left, context);
            op = _variableResolver.ResolveString(op, context);
            right = _variableResolver.ResolveString(right, context);
            resultVariableName = _variableResolver.ResolveString(resultVariableName, context);

            var matched = Evaluate(left, op, right);
            if (!string.IsNullOrWhiteSpace(resultVariableName))
            {
                context.Variables[resultVariableName] = matched;
            }

            var result = StepExecutionResult.Success("条件判断结果：" + matched);
            int nextStepIndex;
            if (matched && int.TryParse(whenTrueStepIndexRaw, out nextStepIndex))
            {
                result.NextStepIndex = nextStepIndex;
            }
            else if (!matched && int.TryParse(whenFalseStepIndexRaw, out nextStepIndex))
            {
                result.NextStepIndex = nextStepIndex;
            }

            return Task.FromResult(result);
        }

        private static bool Evaluate(string left, string op, string right)
        {
            if (string.IsNullOrWhiteSpace(op))
            {
                op = "equals";
            }

            switch (op.Trim().ToLowerInvariant())
            {
                case "equals":
                case "==":
                    return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                case "not_equals":
                case "!=":
                    return !string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                case "contains":
                    return (left ?? string.Empty).IndexOf(right ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
                case "greater_than":
                case ">":
                    return CompareAsNumber(left, right) > 0;
                case "less_than":
                case "<":
                    return CompareAsNumber(left, right) < 0;
                case "is_true":
                    return ParseBoolean(left);
                case "is_false":
                    return !ParseBoolean(left);
                case "exists":
                    return !string.IsNullOrWhiteSpace(left);
                default:
                    return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static int CompareAsNumber(string left, string right)
        {
            double leftValue;
            double rightValue;
            double.TryParse(left, NumberStyles.Any, CultureInfo.InvariantCulture, out leftValue);
            double.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out rightValue);
            return leftValue.CompareTo(rightValue);
        }

        private static bool ParseBoolean(string raw)
        {
            bool value;
            return bool.TryParse(raw, out value) && value;
        }
    }
}
