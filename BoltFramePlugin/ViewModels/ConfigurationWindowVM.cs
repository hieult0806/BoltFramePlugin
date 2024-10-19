using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using BoltFramePlugin.Services;
using System.Windows.Input;

namespace BoltFramePlugin.ViewModels
{
    public class ConfigurationWindowVM : IWindowViewModel
    {
        public ICommand SaveCommand { get; set; }
        public ICommand CancelCommand { get; set; }

        public bool DialogResult { get; set; }

        public event EventHandler RequestClose;

        public string WorkingSpacePath { get; set; }

        private IWindowManager _windowManager;
        private IPluginConfigurationManager _pluginConfiguration;

        public ConfigurationWindowVM(UIDocument document)
        {
            _windowManager = ContainerConfigurator.Container.GetInstance<IWindowManager>();
            _pluginConfiguration = ContainerConfigurator.Container.GetInstance<IPluginConfigurationManager>();

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);

            WorkingSpacePath = _pluginConfiguration.LoadPluginConfiguration().WorkingFolderPath;
        }

        private void Cancel(object obj)
        {
            DialogResult = false;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        private void Save(object obj)
        {
            DialogResult = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }
}
