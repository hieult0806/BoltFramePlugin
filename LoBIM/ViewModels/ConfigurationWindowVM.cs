using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using LoBIM.Services;
using System.Windows.Input;

namespace LoBIM.ViewModels
{
    public class ConfigurationWindowVM : BaseViewModel
    {
        public ICommand CloseCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand CancelCommand { get; set; }
        public string WorkingSpacePath { get; set; }

        private IPluginConfigurationManager _pluginConfiguration;

        public ConfigurationWindowVM(UIDocument document) : base(document)
        {
            _windowManager = DIContainerService.Container.GetInstance<IWindowManager>();
            _pluginConfiguration = DIContainerService.Container.GetInstance<IPluginConfigurationManager>();

            CloseCommand = new RelayCommand(ExecuteCloseCommand);
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);

            WorkingSpacePath = _pluginConfiguration.LoadPluginConfiguration().WorkingFolderPath;
        }

        private void ExecuteCloseCommand(object obj)
        {
            OnRequestClose(EventArgs.Empty);
        }

        private void Cancel(object obj)
        {
            DialogResult = false;
            OnRequestClose(EventArgs.Empty);
        }

        private void Save(object obj)
        {
            DialogResult = true;
            OnRequestClose(EventArgs.Empty);
        }
    }
}
