using System.Windows.Input;
using Autodesk.Revit.DB;
using BoltFramePlugin.Services;
using Autodesk.Revit.UI;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using BoltFramePlugin.Helpers;

namespace BoltFramePlugin.ViewModels
{
    public class TypeSelectionPopupVM : BaseViewModel
    {
        public ICommand ItemDoubleClick { get; set; }
        public ICommand ConfirmCommand { get; set; }
        public ICommand CancelCommand { get; set; }

        public ObservableCollection<CustomElement> Elements { get; set; }
        public CustomElement SelectedItem { get; set; }

        public TypeSelectionPopupVM(UIDocument document, IList<FamilySymbol> elements) : base(document)
        {
            _windowManager = DIContainerService.Container.GetInstance<IWindowManager>();

            ItemDoubleClick = new RelayCommand(SelectElement);
            ConfirmCommand = new RelayCommand(Confirm);
            CancelCommand = new RelayCommand(Cancel);

            Elements = new ObservableCollection<CustomElement>();
            foreach (FamilySymbol familySymbol in elements)
            {
                Elements.Add(new CustomElement(familySymbol));
            }
        }

        public void SelectElement(object parameter)
        {

        }
        public void Confirm(object parameter)
        {
            DialogResult = true;
            OnRequestClose(EventArgs.Empty);
        }
        public void Cancel(object parameter)
        {
            DialogResult = false;
            OnRequestClose(EventArgs.Empty);
        }
    }

    public class CustomElement
    {
        public string Name { get; set; }
        public BitmapSource PreviewImage { get; set; }
        public FamilySymbol Symbol { get; set; }

        public CustomElement(FamilySymbol symbol)
        {
            Name = $"{symbol.FamilyName} - {symbol.Name}";
            PreviewImage = ImageHelpers.BitmapToImageSource(symbol.GetPreviewImage(new Size(64, 64)));
            Symbol = symbol;
        }
    }
}
