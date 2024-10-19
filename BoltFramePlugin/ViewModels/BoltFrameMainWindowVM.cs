using System.Collections.ObjectModel;
using System.ComponentModel;
using BoltFramePlugin.Models;
using System.Windows.Input;
using BoltFramePlugin.Services;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.EventHandlers;
using BoltFramePlugin.FramingStrategies;

namespace BoltFramePlugin.ViewModels
{
    internal class BoltFrameMainWindowVM : BaseViewModel
    {
        private ExternalEvent _externalGenerateEvent;
        private GenerateFrameEventHandler _generateEventHandler;

        private ExternalEvent _externalCreateBeamPocketEvent;
        private CreateBeamPocketEventHandler _createBeamPocketEventHandler;

        // ObservableCollection for attributes
        public ObservableCollection<AttributeItem> Attributes { get; set; }

        // Commands
        public ICommand CloseCommand { get; }
        public ICommand GenerateFrameCommand { get; }
        public ICommand ColumnTypeCommand { get; }
        public ICommand BeamTypeCommand { get; }
        public ICommand OpenConfigurationWindowCommand { get; }
        public ICommand CreateBeamPocketCommand { get; }

        private IRevitService _revitService;

        public BoltFrameMainWindowVM(UIDocument document) : base(document)
        {
            _revitService = DIContainerService.Container.GetInstance<IRevitServiceFactory>().Create(_document);

            _generateEventHandler = new GenerateFrameEventHandler();
            _externalGenerateEvent = ExternalEvent.Create(_generateEventHandler);

            _createBeamPocketEventHandler = new CreateBeamPocketEventHandler();
            _externalCreateBeamPocketEvent = ExternalEvent.Create(_createBeamPocketEventHandler);

            // Initialize commands
            CloseCommand = new RelayCommand(OnClose);
            GenerateFrameCommand = new RelayCommand(GenerateFrame);
            ColumnTypeCommand = new RelayCommand(OnColumnTypeSelect);
            BeamTypeCommand = new RelayCommand(OnBeamTypeSelect);
            OpenConfigurationWindowCommand = new RelayCommand(OpenConfigurationWindow);
            CreateBeamPocketCommand = new RelayCommand(CreateBeamPocket);

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
        private void OnClose(object parameter)
        {
            base.OnRequestClose(EventArgs.Empty);
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
            _externalGenerateEvent.Raise();
        }

        private void OnColumnTypeSelect(object parameter)
        {
            var viewModel = new TypeSelectionPopupVM(_document, _revitService.LoadColumnTypesFromRevit());
            if (_windowManager.OpenDialog(viewModel))
            {
                TextboxRebinding(parameter, viewModel.SelectedItem.Symbol);
            }
        }

        private void OnBeamTypeSelect(object parameter)
        {
            var viewModel = new TypeSelectionPopupVM(_document, _revitService.LoadBeamTypesFromRevit());
            if (_windowManager.OpenDialog(viewModel))
            {
                TextboxRebinding(parameter, viewModel.SelectedItem.Symbol);
            }
        }

        private void OpenConfigurationWindow(object parameter)
        {
            _windowManager.OpenDialog(new ConfigurationWindowVM(_document));
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

        private void CreateBeamPocket(object parameter)
        {
            _createBeamPocketEventHandler.SetParameters(_revitService);
            _externalCreateBeamPocketEvent.Raise();
        }
    }
}
