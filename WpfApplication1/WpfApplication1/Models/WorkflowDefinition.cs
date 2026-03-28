using System;
using System.Collections.ObjectModel;
using System.Linq;
using WpfApplication1.Common;
using WpfApplication1.Enums;

namespace WpfApplication1.Models
{
    public class WorkflowDefinition : BindableBase
    {
        private string _id;
        private string _name;
        private string _version = "0.1.0";
        private ObservableCollection<WorkflowStep> _steps = new ObservableCollection<WorkflowStep>();
        private WorkflowType _workflowType = WorkflowType.General;
        private string _description;
        private string _applicableRole;
        private DateTime? _lastModifiedAt;
        private bool _isPublished;
        private CanvasLayout _canvasLayout = new CanvasLayout();

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

        public WorkflowType WorkflowType
        {
            get { return _workflowType; }
            set { SetProperty(ref _workflowType, value); }
        }

        public string Description
        {
            get { return _description; }
            set { SetProperty(ref _description, value); }
        }

        public string ApplicableRole
        {
            get { return _applicableRole; }
            set { SetProperty(ref _applicableRole, value); }
        }

        public DateTime? LastModifiedAt
        {
            get { return _lastModifiedAt; }
            set { SetProperty(ref _lastModifiedAt, value); }
        }

        public bool IsPublished
        {
            get { return _isPublished; }
            set { SetProperty(ref _isPublished, value); }
        }

        public CanvasLayout CanvasLayout
        {
            get { return _canvasLayout; }
            set { SetProperty(ref _canvasLayout, value); }
        }

        public void EnsureCanvasLayout()
        {
            if (CanvasLayout == null)
            {
                CanvasLayout = new CanvasLayout();
            }

            CanvasLayout.EnsureLinearLayout(Steps ?? new ObservableCollection<WorkflowStep>());
        }
    }
}
