using System.Windows.Input;
using Autodesk.Revit.DB;
using LoBIM.Services;
using Autodesk.Revit.UI;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using LoBIM.Helpers;
using LoBIM.ViewModels.Components;

namespace LoBIM.ViewModels
{
    public class TypeSelectionPopupVM : BaseViewModel
    {
        public ICommand ItemDoubleClick { get; set; }
        public ICommand ConfirmCommand { get; set; }
        public ICommand CancelCommand { get; set; }

        public ObservableCollection<ElementPreviewVM> Elements { get; set; }
        public ElementPreviewVM SelectedItem { get; set; }
        public IRevitService _revitService { get; set; }
        public TypeSelectionPopupVM(IRevitService revitService, IWindowManager windowManager, IList<FamilySymbol> elements) : base(revitService.UiDoc)
        {
            _revitService = revitService;
            _windowManager = windowManager;

            ItemDoubleClick = new RelayCommand(SelectElement);
            ConfirmCommand = new RelayCommand(Confirm);
            CancelCommand = new RelayCommand(Cancel);

            Elements = new ObservableCollection<ElementPreviewVM>();
            foreach (FamilySymbol familySymbol in elements)
            {
                // Disable selection for items in the popup to prevent nested popups
                Elements.Add(new ElementPreviewVM(_revitService, _windowManager, familySymbol, enableSelection: false));
            }
        }

        public void SelectElement(object parameter)
        {
            // When double-clicked, confirm the selection and close
            if (SelectedItem != null)
            {
                DialogResult = true;
                OnRequestClose(EventArgs.Empty);
            }
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
}
