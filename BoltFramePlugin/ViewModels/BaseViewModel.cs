using Autodesk.Revit.UI;
using BoltFramePlugin.Services;
using System.ComponentModel;

namespace BoltFramePlugin.ViewModels
{
    /// <summary>
    /// Interface representing a ViewModel that can request its associated window to close.
    /// </summary>
    public interface IWindowViewModel
    {
        event EventHandler RequestClose;
        bool DialogResult { get; set; }
    }
    public class BaseViewModel : IWindowViewModel, INotifyPropertyChanged
    {
        public bool DialogResult { get; set; }

        public event EventHandler RequestClose;
        public event PropertyChangedEventHandler? PropertyChanged;

        protected UIDocument _document;
        protected IWindowManager _windowManager;

        public BaseViewModel(UIDocument document)
        {
            _document = document;
            _windowManager = DIContainerService.Container.GetInstance<IWindowManager>();
        }

        protected void OnRequestClose(EventArgs eventArgs)
        {
            RequestClose?.Invoke(this, eventArgs);
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
