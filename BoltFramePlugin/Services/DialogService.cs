using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB;
using BoltFramePlugin.ViewModels;

namespace BoltFramePlugin.Services
{
    public interface IDialogService
    {
        bool OpenElementSelectPopup(string title, out FamilySymbol symbol, IList<FamilySymbol> beamTypes);
        bool ConfirmAndClose();
        bool CancelAndClose();
    }
    public class DialogService : IDialogService
    {
        private readonly IRevitService _revitService;

        private Window _currentWindow;

        public DialogService(IRevitService revitService)
        {
            _revitService = revitService;
        }

        public bool OpenElementSelectPopup(string title, out FamilySymbol symbol, IList<FamilySymbol> beamTypes)
        {
            try
            {
                _currentWindow = new TypeSelectionWindow(this, beamTypes, title);
                if (_currentWindow.ShowDialog() == true)
                {
                    symbol = (_currentWindow.DataContext as TypeSelectionPopupViewModel).SelectedItem.Symbol;
                    return true;
                }

                symbol = null;
                return false;
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Error", $"{ex.Message}");
                symbol = null;
                return false;
            }
        }

        public bool ConfirmAndClose()
        {
            return Close(true);
        }

        public bool CancelAndClose()
        {
            return Close(false);
        }

        private bool Close(bool result)
        {
            if (_currentWindow != null)
            {
                _currentWindow.DialogResult = result;
                _currentWindow.Close();
                return true;
            }
            return false;
        }
    }
}
