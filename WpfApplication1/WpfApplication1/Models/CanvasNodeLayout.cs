using WpfApplication1.Common;

namespace WpfApplication1.Models
{
    public class CanvasNodeLayout : BindableBase
    {
        private string _stepId;
        private double _x;
        private double _y;
        private double _width = 200;
        private double _height = 88;
        private string _visualGroup;

        public string StepId
        {
            get { return _stepId; }
            set { SetProperty(ref _stepId, value); }
        }

        public double X
        {
            get { return _x; }
            set { SetProperty(ref _x, value); }
        }

        public double Y
        {
            get { return _y; }
            set { SetProperty(ref _y, value); }
        }

        public double Width
        {
            get { return _width; }
            set { SetProperty(ref _width, value); }
        }

        public double Height
        {
            get { return _height; }
            set { SetProperty(ref _height, value); }
        }

        public string VisualGroup
        {
            get { return _visualGroup; }
            set { SetProperty(ref _visualGroup, value); }
        }
    }
}
