using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Helpers;
using BoltFramePlugin.Services;
using BoltFramePlugin.ViewModels.Framing;
using BoltFramePlugin.Views.Components;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace BoltFramePlugin.ViewModels.Components
{
    public class ElementPreviewVM : INotifyPropertyChanged
    {
        public ICommand SelectCommand { get; set; }
        public FamilySymbol Symbol { get; set; }

        private BitmapSource _previewImage;
        public BitmapSource PreviewImage { get => _previewImage; set { _previewImage = value; OnPropertyChanged(nameof(PreviewImage)); } }

        private string _familyName;
        public string FamilyName { get => _familyName; set { _familyName = value; OnPropertyChanged(nameof(FamilyName)); } }

        private string _elementName;
        public string ElementName { get => _elementName; set { _elementName = value; OnPropertyChanged(nameof(ElementName)); } }

        public event PropertyChangedEventHandler? PropertyChanged;

        private IRevitService _revitService;
        private IWindowManager _windowManager;

        public ElementPreviewVM(IRevitService revitService, IWindowManager windowManager, FamilySymbol symbol)
        {
            _revitService = revitService;
            _windowManager = windowManager;
            Symbol = symbol;
            FamilyName = symbol.FamilyName;
            ElementName = symbol.Name;
            PreviewImage = ImageHelpers.BitmapToImageSource(symbol.GetPreviewImage(new Size(64, 64)));
            SelectCommand = new RelayCommand(OnSelect);
        }

        private void OnSelect(object obj)
        {
            TypeSelectionPopupVM popupVM = new TypeSelectionPopupVM(_revitService, _windowManager, _revitService.LoadBeamTypesFromRevit());
            if (_windowManager.OpenDialog(popupVM))
            {
                PreviewImage = popupVM.SelectedItem.PreviewImage;
                FamilyName = popupVM.SelectedItem.FamilyName;
                ElementName = popupVM.SelectedItem.ElementName;
                Symbol = popupVM.SelectedItem.Symbol;
            }
        }
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
