using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace LoBIM.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            // Set Revit as owner
            var revitWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (revitWindow != IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(this);
                helper.Owner = revitWindow;
            }

            // Load SAIT logo
            LoadSaitLogo();

            // Set build date
            DataContext = new AboutWindowViewModel();

            // Focus back to Revit when window closes
            Closed += (s, e) =>
            {
                if (revitWindow != IntPtr.Zero)
                {
                    SetForegroundWindow(revitWindow);
                }
            };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }
        }

        private void LoadSaitLogo()
        {
            try
            {
                // Get the directory where the DLL is located
                var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyPath);
                var logoPath = Path.Combine(assemblyDir, "Resources", "Images", "sait_logo.png");

                if (File.Exists(logoPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    SaitLogoImage.Source = bitmap;
                }
            }
            catch
            {
                // If logo fails to load, just leave it empty
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }

    public class AboutWindowViewModel
    {
        public string BuildDate { get; }

        public AboutWindowViewModel()
        {
            // Get build date from assembly
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var buildDate = System.IO.File.GetLastWriteTime(assembly.Location);
            BuildDate = buildDate.ToString("MMMM dd, yyyy");
        }
    }
}
