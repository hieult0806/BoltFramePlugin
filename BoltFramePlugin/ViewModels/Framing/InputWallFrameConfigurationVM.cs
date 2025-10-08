using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.FramingStrategies;
using BoltFramePlugin.Helpers;
using BoltFramePlugin.Services;
using BoltFramePlugin.ViewModels.Components;
using BoltFramePlugin.Views.Components;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace BoltFramePlugin.ViewModels.Framing
{
    public class InputWallFrameConfigurationVM : BaseViewModel
    {
        public bool DialogResult { get; set; }

        public event EventHandler RequestClose;

        public GridConfig Config { get; set; }

        private IRevitService _revitService;
        private IWindowManager _windowManager;
        public IList<FamilySymbol> Beams;

        public string PartName { get; set; }
        public bool IsBoundary => PartName?.Contains("Boundary") ?? false;

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        private ElementPreviewVM _elementPreview;
        public ElementPreviewVM ElementPreview
        {
            get => _elementPreview;
            set
            {
                _elementPreview = value;
                OnPropertyChanged(nameof(_elementPreview));
            }
        }

        public InputWallFrameConfigurationVM(IRevitService revitService, IWindowManager windowManager, string partName) : base(revitService.UiDoc)
        {
            _revitService = revitService;
            _windowManager = windowManager;
            PartName = partName;

            Binding();
        }

        public void Binding()
        {
            Config = new GridConfig();
            Config.ZOffset = -300; // Default -300mm places beams below floor surface
            Config.StructuralType = Autodesk.Revit.DB.Structure.StructuralType.Beam; // Set proper structural type for beams
            Beams = _revitService.LoadBeamTypesFromRevit();
            ElementPreview = new ElementPreviewVM(_revitService, _windowManager, Beams.First());
        }

        public void SelectSymbol(object parameter)
        {

        }
    }
}
