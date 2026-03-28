using System.Windows;
using System.Windows.Controls;
using WpfApplication1.Models;
using WpfApplication1.ViewModels;

namespace WpfApplication1
{
    public class StepParameterEditorTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TextParameterTemplate { get; set; }

        public DataTemplate MultiLineParameterTemplate { get; set; }

        public DataTemplate BooleanParameterTemplate { get; set; }

        public DataTemplate SelectParameterTemplate { get; set; }

        public DataTemplate XPathParameterTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var parameter = item as StepParameterViewModel;
            if (parameter == null)
            {
                return TextParameterTemplate;
            }

            switch (parameter.EditorKind)
            {
                case StepParameterEditorKind.Boolean:
                    return BooleanParameterTemplate ?? TextParameterTemplate;
                case StepParameterEditorKind.Select:
                    return SelectParameterTemplate ?? TextParameterTemplate;
                case StepParameterEditorKind.MultiLine:
                    return MultiLineParameterTemplate ?? TextParameterTemplate;
                case StepParameterEditorKind.XPath:
                    return XPathParameterTemplate ?? TextParameterTemplate;
                default:
                    return TextParameterTemplate;
            }
        }
    }
}
