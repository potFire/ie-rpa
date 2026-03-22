using System;
using System.Reflection;
using System.Runtime.InteropServices;
using mshtml;

namespace WpfApplication1.Automation.IE
{
    public class IeElement : IIeElement
    {
        private readonly IHTMLElement _element;

        public IeElement(IHTMLElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            _element = element;
        }

        public string Text
        {
            get
            {
                var innerText = _element.innerText;
                if (!string.IsNullOrWhiteSpace(innerText))
                {
                    return innerText;
                }

                return Value;
            }
        }

        public string Value
        {
            get { return Convert.ToString(_element.getAttribute("value", 0)); }
        }

        public void Click()
        {
            var element2 = _element as IHTMLElement2;
            if (element2 != null)
            {
                TryFocus(element2);
            }

            InvokeMethod("click");
        }

        public void SetValue(string value)
        {
            var element2 = _element as IHTMLElement2;
            if (element2 != null)
            {
                TryFocus(element2);
            }

            var inputElement = _element as IHTMLInputElement;
            if (inputElement != null)
            {
                inputElement.value = value;
                FireCommonEvents();
                return;
            }

            var textAreaElement = _element as IHTMLTextAreaElement;
            if (textAreaElement != null)
            {
                textAreaElement.value = value;
                FireCommonEvents();
                return;
            }

            var selectElement = _element as IHTMLSelectElement;
            if (selectElement != null)
            {
                selectElement.value = value;
                FireCommonEvents();
                return;
            }

            _element.setAttribute("value", value, 0);
            FireCommonEvents();
        }

        public string GetAttribute(string name)
        {
            return Convert.ToString(_element.getAttribute(name, 0));
        }

        public void SelectOption(string optionValueOrText, bool byText)
        {
            var selectElement = _element as IHTMLSelectElement;
            if (selectElement == null)
            {
                throw new InvalidOperationException("当前元素不是下拉框，无法执行选择操作。");
            }

            var optionsObject = selectElement.options;
            if (optionsObject == null)
            {
                throw new InvalidOperationException("当前下拉框没有可用选项。");
            }

            var optionsType = optionsObject.GetType();
            var lengthObject = optionsType.InvokeMember("length", BindingFlags.GetProperty, null, optionsObject, null);
            var length = Convert.ToInt32(lengthObject);

            for (var i = 0; i < length; i++)
            {
                object index = i;
                var optionObject = optionsType.InvokeMember("item", BindingFlags.InvokeMethod, null, optionsObject, new[] { index });
                if (optionObject == null)
                {
                    continue;
                }

                var optionType = optionObject.GetType();
                var text = Convert.ToString(optionType.InvokeMember("text", BindingFlags.GetProperty, null, optionObject, null));
                var value = Convert.ToString(optionType.InvokeMember("value", BindingFlags.GetProperty, null, optionObject, null));
                var candidate = byText ? text : value;

                if (string.Equals(candidate ?? string.Empty, optionValueOrText ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    selectElement.selectedIndex = i;
                    FireCommonEvents();
                    return;
                }
            }

            throw new InvalidOperationException("未找到匹配的下拉选项：" + optionValueOrText);
        }

        private void FireCommonEvents()
        {
            TryInvokeMethod("fireEvent", "onpropertychange", null);
            TryInvokeMethod("fireEvent", "oninput", null);
            TryInvokeMethod("fireEvent", "onchange", null);

            var element2 = _element as IHTMLElement2;
            if (element2 != null)
            {
                TryBlur(element2);
                return;
            }

            TryInvokeMethod("fireEvent", "onblur", null);
        }

        private void InvokeMethod(string methodName, params object[] args)
        {
            _element.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                null,
                _element,
                args);
        }

        private void TryInvokeMethod(string methodName, params object[] args)
        {
            try
            {
                InvokeMethod(methodName, args);
            }
            catch (Exception ex)
            {
                if (!IsIgnorableDomInvocationError(ex))
                {
                    throw;
                }
            }
        }

        private static void TryFocus(IHTMLElement2 element)
        {
            try
            {
                element.focus();
            }
            catch (COMException)
            {
            }
        }

        private static void TryBlur(IHTMLElement2 element)
        {
            try
            {
                element.blur();
            }
            catch (COMException)
            {
            }
        }

        private static bool IsIgnorableDomInvocationError(Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                var comException = current as COMException;
                if (comException != null
                    && (comException.HResult == unchecked((int)0x80020006)
                        || comException.HResult == unchecked((int)0x80020003)
                        || comException.HResult == unchecked((int)0x80004001)))
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }
    }
}
