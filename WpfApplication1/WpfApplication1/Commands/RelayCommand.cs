using System;
using System.Windows.Input;

namespace WpfApplication1.Commands
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        private readonly Action<object> _executeWithParameter;
        private readonly Func<object, bool> _canExecuteWithParameter;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            if (execute == null)
            {
                throw new ArgumentNullException("execute");
            }

            _execute = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            if (execute == null)
            {
                throw new ArgumentNullException("execute");
            }

            _executeWithParameter = execute;
            _canExecuteWithParameter = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            if (_executeWithParameter != null)
            {
                return _canExecuteWithParameter == null || _canExecuteWithParameter(parameter);
            }

            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            if (_executeWithParameter != null)
            {
                _executeWithParameter(parameter);
                return;
            }

            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}