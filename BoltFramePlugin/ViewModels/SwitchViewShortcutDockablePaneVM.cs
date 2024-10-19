using BoltFramePlugin.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BoltFramePlugin.ViewModels
{
    public class SwitchViewShortcutDockablePaneVM : IWindowViewModel, INotifyPropertyChanged
    {
        public bool DialogResult { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler RequestClose;

        public SwitchViewShortcutDockablePaneVM()
        {

        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
