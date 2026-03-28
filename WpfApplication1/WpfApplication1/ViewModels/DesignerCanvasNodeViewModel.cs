using WpfApplication1.Common;
using WpfApplication1.Models;

namespace WpfApplication1.ViewModels
{
    public class DesignerCanvasNodeViewModel : BindableBase
    {
        private readonly WorkflowStep _step;
        private readonly CanvasNodeLayout _layout;
        private bool _isSelected;

        public DesignerCanvasNodeViewModel(WorkflowStep step, CanvasNodeLayout layout)
        {
            _step = step;
            _layout = layout;
        }

        public WorkflowStep Step
        {
            get { return _step; }
        }

        public string StepId
        {
            get { return _step != null ? _step.Id : string.Empty; }
        }

        public string Name
        {
            get { return _step != null ? _step.Name : string.Empty; }
        }

        public string StepTypeText
        {
            get { return _step != null ? _step.StepType.ToString() : string.Empty; }
        }

        public double X
        {
            get { return _layout != null ? _layout.X : 0; }
            set
            {
                if (_layout == null || _layout.X == value)
                {
                    return;
                }

                _layout.X = value;
                OnPropertyChanged("X");
            }
        }

        public double Y
        {
            get { return _layout != null ? _layout.Y : 0; }
            set
            {
                if (_layout == null || _layout.Y == value)
                {
                    return;
                }

                _layout.Y = value;
                OnPropertyChanged("Y");
            }
        }

        public double Width
        {
            get { return _layout != null ? _layout.Width : 200; }
        }

        public double Height
        {
            get { return _layout != null ? _layout.Height : 88; }
        }

        public string Category
        {
            get { return MainWindowViewModel.GetStepCategory(_step != null ? _step.StepType : 0); }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }
    }
}
