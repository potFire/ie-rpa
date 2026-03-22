using System.Collections.ObjectModel;
using WpfApplication1.Common;

namespace WpfApplication1.Models
{
    public class WorkflowDefinition : BindableBase
    {
        private string _id;
        private string _name;
        private string _version = "0.1.0";
        private ObservableCollection<WorkflowStep> _steps = new ObservableCollection<WorkflowStep>();

        public string Id
        {
            get { return _id; }
            set { SetProperty(ref _id, value); }
        }

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        public string Version
        {
            get { return _version; }
            set { SetProperty(ref _version, value); }
        }

        public ObservableCollection<WorkflowStep> Steps
        {
            get { return _steps; }
            set { SetProperty(ref _steps, value); }
        }
    }
}
