using System;
using System.Collections.Generic;

namespace WpfApplication1.Models
{
    public class StepExecutionResult
    {
        public StepExecutionResult()
        {
            Outputs = new Dictionary<string, object>();
        }

        public bool IsSuccess { get; set; }

        public string Message { get; set; }

        public Exception Exception { get; set; }

        public TimeSpan Duration { get; set; }

        public Dictionary<string, object> Outputs { get; set; }

        // 如果流程步骤需要修改执行位置，就通过这个字段告诉运行器下一步跳到哪里。
        // 这里使用 0 基索引，和流程列表本身保持一致，便于后续做条件跳转和循环回跳。
        public int? NextStepIndex { get; set; }

        public static StepExecutionResult Success(string message = null)
        {
            return new StepExecutionResult
            {
                IsSuccess = true,
                Message = message
            };
        }

        public static StepExecutionResult Failure(string message, Exception exception = null)
        {
            return new StepExecutionResult
            {
                IsSuccess = false,
                Message = message,
                Exception = exception
            };
        }
    }
}
