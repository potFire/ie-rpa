using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Selectors;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class UploadFileStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;

        public UploadFileStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
        }

        public StepType StepType
        {
            get { return StepType.UploadFile; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            var page = context.CurrentPage as IIePage;
            if (page == null)
            {
                throw new InvalidOperationException("当前没有可上传文件的 IE 页面。");
            }

            string selectorRaw;
            string clickSelectorRaw;
            string filePath;
            string dialogDelayRaw;

            step.Parameters.TryGetValue("selector", out selectorRaw);
            step.Parameters.TryGetValue("clickSelector", out clickSelectorRaw);
            step.Parameters.TryGetValue("filePath", out filePath);
            step.Parameters.TryGetValue("dialogDelayMs", out dialogDelayRaw);

            filePath = _variableResolver.ResolveString(filePath, context);
            selectorRaw = _variableResolver.ResolveString(selectorRaw, context);
            clickSelectorRaw = _variableResolver.ResolveString(clickSelectorRaw, context);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return StepExecutionResult.Failure("未配置 filePath 参数。");
            }

            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.GetFullPath(filePath);
            }

            if (!File.Exists(filePath))
            {
                return StepExecutionResult.Failure("上传文件不存在：" + filePath);
            }

            // 第一优先级：直接定位 file input 并写入路径。
            // 这是最稳定、最不依赖桌面焦点的做法。
            if (!string.IsNullOrWhiteSpace(selectorRaw))
            {
                try
                {
                    var selector = SelectorParser.Parse(selectorRaw);
                    var fileInput = page.FindElement(selector);
                    fileInput.SetValue(filePath);
                    return StepExecutionResult.Success("文件已写入上传控件。" + filePath);
                }
                catch (Exception ex)
                {
                    // 如果直写失败，再尝试“点击上传按钮 + SendKeys”兜底。
                    if (string.IsNullOrWhiteSpace(clickSelectorRaw))
                    {
                        return StepExecutionResult.Failure("写入文件上传控件失败：" + ex.Message, ex);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(clickSelectorRaw))
            {
                return StepExecutionResult.Failure("未提供 selector，也未提供 clickSelector，无法完成文件上传。");
            }

            var clickSelector = SelectorParser.Parse(clickSelectorRaw);
            var trigger = page.FindElement(clickSelector);
            trigger.Click();

            var dialogDelayMs = 800;
            int parsedDelay;
            if (int.TryParse(dialogDelayRaw, out parsedDelay) && parsedDelay >= 0)
            {
                dialogDelayMs = parsedDelay;
            }

            await Task.Delay(dialogDelayMs);

            // 兜底策略说明：
            // 某些老式 IE 页面不会暴露 file input，而是自己弹出系统文件选择框。
            // 这时只能依赖焦点窗口，向原生对话框发送文件路径和回车键。
            SendKeys.SendWait(filePath);
            Thread.Sleep(150);
            SendKeys.SendWait("{ENTER}");

            return StepExecutionResult.Success("已尝试通过文件对话框上传文件。" + filePath);
        }
    }
}
