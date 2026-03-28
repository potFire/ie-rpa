using System.Collections.ObjectModel;
using WpfApplication1.Models;

namespace WpfApplication1.ViewModels
{
    public class StepParameterGroupViewModel
    {
        public StepParameterGroupViewModel(StepParameterSection section, string title, ObservableCollection<StepParameterViewModel> parameters)
        {
            Section = section;
            Title = title;
            Parameters = parameters ?? new ObservableCollection<StepParameterViewModel>();
        }

        public StepParameterSection Section { get; private set; }

        public string Title { get; private set; }

        public ObservableCollection<StepParameterViewModel> Parameters { get; private set; }
    }
}
