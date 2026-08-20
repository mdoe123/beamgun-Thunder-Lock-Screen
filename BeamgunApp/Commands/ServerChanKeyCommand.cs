using System;
using System.Windows.Input;
using BeamgunApp.ViewModel;

namespace BeamgunApp.Commands
{
    public class ServerChanKeyCommand : ICommand
    {
        private readonly BeamgunViewModel _viewModel;

        public ServerChanKeyCommand(BeamgunViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            _viewModel.ModifySendKey();
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}