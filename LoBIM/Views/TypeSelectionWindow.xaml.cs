using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using LoBIM.Services;
using LoBIM.ViewModels;

namespace LoBIM
{
    /// <summary>
    /// Interaction logic for TypeSelectionWindow.xaml
    /// </summary>
    public partial class TypeSelectionWindow : Window
    {
        public TypeSelectionWindow()
        {
            InitializeComponent();
        }
    }
}
