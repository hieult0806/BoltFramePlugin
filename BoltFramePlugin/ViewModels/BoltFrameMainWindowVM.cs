using System.Collections.ObjectModel;
using System.ComponentModel;
using BoltFramePlugin.Models;
using System.Windows.Input;
using BoltFramePlugin.Services;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.EventHandlers;
using BoltFramePlugin.FramingStrategies;
using BoltFramePlugin.ViewModels.Framing;
using BoltFramePlugin.ViewModels.UserControls;

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
        public ICommand OpenConfigurationWindowCommand { get; }
        public ICommand CreateBeamPocketCommand { get; }

        private IRevitService _revitService;

        private InputWallFrameConfigurationVM _horizontalFrameConfiguration;
        public InputWallFrameConfigurationVM HorizontalFrameConfiguration
        {
            get => _horizontalFrameConfiguration;
            set
            {
                _horizontalFrameConfiguration = value;
                OnPropertyChanged(nameof(HorizontalFrameConfiguration));
            }
        }

        private InputWallFrameConfigurationVM _verticalFrameConfiguration;
        public InputWallFrameConfigurationVM VerticalFrameConfiguration
        {
            get => _verticalFrameConfiguration;
            set
            {
                _verticalFrameConfiguration = value;
                OnPropertyChanged(nameof(VerticalFrameConfiguration));
            }
        }

        private TreeViewUcVM _treeViewUcVM;
        public TreeViewUcVM TreeViewUcVM { get => _treeViewUcVM; set { _treeViewUcVM = value; OnPropertyChanged(nameof(TreeViewUcVM)); } }

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
            OpenConfigurationWindowCommand = new RelayCommand(OpenConfigurationWindow);
            CreateBeamPocketCommand = new RelayCommand(CreateBeamPocket);

            HorizontalFrameConfiguration = new InputWallFrameConfigurationVM(_revitService, _windowManager, "Horizontal Beam");
            VerticalFrameConfiguration = new InputWallFrameConfigurationVM(_revitService, _windowManager, "Vertical Beam");
            TreeViewUcVM = new TreeViewUcVM(_revitService.UiDoc);
            TreeViewUcVM.PropertyChanged += TreeViewUcVM_PropertyChanged;
        }

        private void TreeViewUcVM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {

        }

        private void OnClose(object parameter)
        {
            DialogResult = false;
            OnRequestClose(EventArgs.Empty);
        }

        private void GenerateFrame(object parameter)
        {
            //_generateEventHandler.SetParameters(_revitService, floorModel);
            //_externalGenerateEvent.Raise();
        }

        private void OpenConfigurationWindow(object parameter)
        {
            _windowManager.OpenDialog(new ConfigurationWindowVM(_document));
        }

        private void CreateBeamPocket(object parameter)
        {
            _createBeamPocketEventHandler.SetParameters(_revitService);
            _externalCreateBeamPocketEvent.Raise();
        }
    }
}
