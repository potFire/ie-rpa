using WpfApplication1.Common;
using WpfApplication1.Enums;

namespace WpfApplication1.Models
{
    public class WorkflowStep : BindableBase
    {
        private string _id;
        private string _name;
        private StepType _stepType;
        private int _timeoutMs = 10000;
        private int _retryCount;
        private bool _continueOnError;
        private StepParameterBag _parameters = new StepParameterBag();

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

        public StepType StepType
        {
            get { return _stepType; }
            set { SetProperty(ref _stepType, value); }
        }

        public int TimeoutMs
        {
            get { return _timeoutMs; }
            set { SetProperty(ref _timeoutMs, value); }
        }

        public int RetryCount
        {
            get { return _retryCount; }
            set { SetProperty(ref _retryCount, value); }
        }

        public bool ContinueOnError
        {
            get { return _continueOnError; }
            set { SetProperty(ref _continueOnError, value); }
        }

        public StepParameterBag Parameters
        {
            get { return _parameters; }
            set { SetProperty(ref _parameters, value); }
        }
    }
}
