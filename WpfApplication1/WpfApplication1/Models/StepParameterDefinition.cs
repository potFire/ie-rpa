using System.Collections.Generic;

namespace WpfApplication1.Models
{
    public class StepParameterDefinition
    {
        public string Key { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public StepParameterEditorKind EditorKind { get; set; }

        public StepParameterSection Section { get; set; }

        public bool IsRequired { get; set; }

        public string DefaultValue { get; set; }

        public bool SupportsPicker { get; set; }

        public int Order { get; set; }

        public IList<StepParameterOption> Options { get; set; }
    }
}
