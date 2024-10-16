using Autodesk.Revit.DB.Visual;
using BoltFramePlugin.Helpers;
using BoltFramePlugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public ConfigurationWindowVM()
        {
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);

            WorkingSpacePath = ConfigurationManager.ConfigurationFilePath;
        }

        private void Cancel(object obj)
        {

        }

        private void Save(object obj)
        {

        }
    }
}
