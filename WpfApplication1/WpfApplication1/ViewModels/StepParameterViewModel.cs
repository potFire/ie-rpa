using System;
using System.Collections.Generic;
using System.Linq;
using WpfApplication1.Common;
using WpfApplication1.Models;

namespace WpfApplication1.ViewModels
{
    public class StepParameterViewModel : BindableBase
    {
        private readonly WorkflowStep _step;
        private readonly StepParameterDefinition _definition;
        private readonly Action _valueChangedCallback;

        public StepParameterViewModel(WorkflowStep step, StepParameterDefinition definition, Action valueChangedCallback)
        {
            _step = step;
            _definition = definition;
            _valueChangedCallback = valueChangedCallback;
            Options = definition.Options != null
                ? definition.Options.ToList().AsReadOnly()
                : new List<StepParameterOption>().AsReadOnly();
        }

        public string Key
        {
            get { return _definition.Key; }
        }

        public string DisplayName
        {
            get { return _definition.DisplayName; }
        }

        public string DisplayLabel
        {
            get { return _definition.IsRequired ? _definition.DisplayName + " *" : _definition.DisplayName; }
        }

        public string Description
        {
            get { return _definition.Description ?? string.Empty; }
        }

        public bool HasDescription
        {
            get { return !string.IsNullOrWhiteSpace(Description); }
        }

        public StepParameterEditorKind EditorKind
        {
            get { return _definition.EditorKind; }
        }

        public bool SupportsPicker
        {
            get { return _definition.SupportsPicker; }
        }

        public IReadOnlyList<StepParameterOption> Options { get; private set; }

        public string Value
        {
            get
            {
                string value;
                if (_step != null && _step.Parameters != null && _step.Parameters.TryGetValue(Key, out value))
                {
                    return value ?? string.Empty;
                }

                return _definition.DefaultValue ?? string.Empty;
            }
            set
            {
                var normalized = value ?? string.Empty;
                if (string.Equals(Value, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                if (_step.Parameters == null)
                {
                    _step.Parameters = new StepParameterBag();
                }

                _step.Parameters[Key] = normalized;
                OnPropertyChanged("Value");
                OnPropertyChanged("BooleanValue");
                _valueChangedCallback();
            }
        }

        public bool BooleanValue
        {
            get
            {
                bool value;
                return bool.TryParse(Value, out value) && value;
            }
            set
            {
                Value = value ? "true" : "false";
            }
        }
    }
}
