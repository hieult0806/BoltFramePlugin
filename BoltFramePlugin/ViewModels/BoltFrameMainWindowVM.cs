using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoltFramePlugin.Models;
using System.Windows.Input;
using BoltFramePlugin.Services;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.EventHandlers;
using BoltFramePlugin.FramingStrategies;

namespace BoltFramePlugin.ViewModels
{
    internal class BoltFrameMainWindowVM : IWindowViewModel, INotifyPropertyChanged
    {
        private ExternalEvent _externalEvent;
        private GenerateEventHandler _generateEventHandler;

        // ObservableCollection for attributes
        public ObservableCollection<AttributeItem> Attributes { get; set; }

        // Commands
        public ICommand GenerateFrameCommand { get; }
        public ICommand ColumnTypeCommand { get; }
        public ICommand BeamTypeCommand { get; }
        public ICommand OpenConfigurationWindowCommand { get; }
        public bool DialogResult { get; set; }

        private IRevitService _revitService;
        private IWindowManager _windowManager;

        public BoltFrameMainWindowVM(IRevitService revitService, IWindowManager dialogService)
        {
            _generateEventHandler = new GenerateEventHandler();
            _externalEvent = ExternalEvent.Create(_generateEventHandler);

            _revitService = revitService;
            _windowManager = dialogService;

            // Initialize commands
            GenerateFrameCommand = new RelayCommand(GenerateFrame);
            ColumnTypeCommand = new RelayCommand(OnColumnTypeSelect);
            BeamTypeCommand = new RelayCommand(OnBeamTypeSelect);
            OpenConfigurationWindowCommand = new RelayCommand(OpenConfigurationWindow);

            // Initialize attributes
            var defaultBeamType = _revitService.GetDefaultBeamFamilySymbol();
            var defaultColType = _revitService.GetDefaultColumnFamilySymbol();

            var frameConfig1 = new FrameConfigurationDataModel();
            frameConfig1.Attributes["Column Type"] = $"{defaultBeamType.Name}";
            frameConfig1.Attributes["Beam Type"] = $"{defaultColType.Name}";
            Attributes = new ObservableCollection<AttributeItem>(
                frameConfig1.Attributes.Select(attr => new AttributeItem(attr.Key, attr.Value))
            );
        }

        private void GenerateFrame(object parameter)
        {
            var floorModel = new FloorFrameGenerateModel();
            floorModel.TargetElement = _revitService.GetSelectedElement();
            floorModel.Level = _revitService.GetLevelById(floorModel.TargetElement.LevelId);
            floorModel.ColSymbol = _revitService.GetColumnTypeByUniqueId(Attributes.First(c => c.Parameter == "Column Type"));
            floorModel.BeamSymbol = _revitService.GetBeamTypeByUniqueId(Attributes.First(c => c.Parameter == "Beam Type"));
            floorModel.Z_Offset = Convert.ToDouble(Attributes.First(c => c.Parameter.Equals("Z-Offset")).Value);
            floorModel.JoistSpacing = Convert.ToDouble(Attributes.First(c => c.Parameter.Equals("Joist Spacing")).Value);
            floorModel.BeamSpacing = Convert.ToDouble(Attributes.First(c => c.Parameter.Equals("Beam Spacing")).Value);

            _generateEventHandler.SetParameters(_revitService, floorModel);
            _externalEvent.Raise();
        }

        // Implement INotifyPropertyChanged for data binding
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler RequestClose;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Command action for Column Type selection
        private void OnColumnTypeSelect(object parameter)
        {
            var viewModel = new TypeSelectionPopupVM(_windowManager, _revitService.LoadColumnTypesFromRevit());
            if (_windowManager.OpenDialog(viewModel))
            {
                TextboxRebinding(parameter, viewModel.SelectedItem.Symbol);
            }
        }

        // Command action for Beam Type selection
        private void OnBeamTypeSelect(object parameter)
        {
            var viewModel = new TypeSelectionPopupVM(_windowManager, _revitService.LoadBeamTypesFromRevit());
            if (_windowManager.OpenDialog(viewModel))
            {
                TextboxRebinding(parameter, viewModel.SelectedItem.Symbol);
            }
        }

        private void OpenConfigurationWindow(object parameter)
        {
            _windowManager.OpenDialog(ContainerConfigurator.Container.GetInstance<ConfigurationWindowVM>());
        }

        void TextboxRebinding(object parameter, FamilySymbol selectedType)
        {
            if (selectedType == null) return;
            var textBox = parameter as System.Windows.Controls.TextBox;
            if (textBox != null)
            {
                textBox.Text = selectedType.Name;
                var attributeItem = textBox.DataContext as AttributeItem;
                if (attributeItem != null)
                {
                    attributeItem.Value = selectedType.Name; // Update the underlying data
                    attributeItem.UniqueId = selectedType.UniqueId;
                }
            }
        }
    }
}
