using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Autodesk.Revit.DB;
using BoltFramePlugin.Services;
using Autodesk.Revit.UI;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using BoltFramePlugin.Helpers;

namespace BoltFramePlugin.ViewModels
{
    public class TypeSelectionPopupViewModel
    {
        public readonly IDialogService _dialogService;
        public ICommand ItemDoubleClick { get; set; }
        public ICommand ConfirmCommand { get; set; }
        public ICommand CancelCommand { get; set; }

        public ObservableCollection<CustomElement> Elements { get; set; }

        public CustomElement SelectedItem { get; set; }

        public TypeSelectionPopupViewModel(IDialogService dialogService, IList<FamilySymbol> elements)
        {
            _dialogService = dialogService;

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
            if (SelectedItem != null)
            {
                _dialogService.ConfirmAndClose();
            }
            else
            {
                Autodesk.Revit.UI.TaskDialog.Show("Error", "No item has been selected.");
            }
        }
        public void Cancel(object parameter)
        {
            _dialogService.CancelAndClose();
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
