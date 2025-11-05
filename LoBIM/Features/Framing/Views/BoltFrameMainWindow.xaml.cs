using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Xml.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LoBIM.AddInEntryPoint;
using LoBIM.EventHandlers;
using LoBIM.Filters;
using LoBIM.Models;
using LoBIM.Services;
using LoBIM.Features.Framing.ViewModels;
using MessageBox = System.Windows.MessageBox;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using TextBox = System.Windows.Controls.TextBox;

namespace LoBIM.Features.Framing.Views
{
    /// <summary>
    /// Interaction logic for BoltFrameConfiguration.xaml
    /// </summary>
    public partial class BoltFrameMainWindow : Window
    {
        public BoltFrameMainWindow()
        {
            InitializeComponent();
        }
    }
}
