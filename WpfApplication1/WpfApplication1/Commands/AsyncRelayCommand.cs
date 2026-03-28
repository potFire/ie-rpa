using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WpfApplication1.Commands
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool> _canExecute;
        private readonly Func<object, Task> _executeAsyncWithParameter;
        private readonly Func<object, bool> _canExecuteWithParameter;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            if (executeAsync == null)
            {
                throw new ArgumentNullException("executeAsync");
            }

            _executeAsync = executeAsync;
            _canExecute = canExecute;
        }

        public AsyncRelayCommand(Func<object, Task> executeAsync, Func<object, bool> canExecute = null)
        {
            if (executeAsync == null)
            {
                throw new ArgumentNullException("executeAsync");
            }

            _executeAsyncWithParameter = executeAsync;
            _canExecuteWithParameter = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            if (_isExecuting)
            {
                return false;
            }

            if (_executeAsyncWithParameter != null)
            {
                return _canExecuteWithParameter == null || _canExecuteWithParameter(parameter);
            }

            return _canExecute == null || _canExecute();
        }

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                if (_executeAsyncWithParameter != null)
                {
                    await _executeAsyncWithParameter(parameter);
                }
                else
                {
                    await _executeAsync();
                }
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}