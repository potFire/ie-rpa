using System.Collections.Generic;
using WpfApplication1.Models;

namespace WpfApplication1.Automation.IE
{
    public interface IIeElement
    {
        string Text { get; }

        string Value { get; }

        void Click();

        void SetValue(string value);

        string GetAttribute(string name);

        // 下拉框和 file input 都属于特殊元素，它们的写值逻辑和普通 input 不同。
        // 这里单独暴露出选择选项的能力，避免执行器层再去接触具体的 COM 类型。
        void SelectOption(string optionValueOrText, bool byText);
    }
}
