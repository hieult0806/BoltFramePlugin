using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using BoltFramePlugin.Models.LimitingDistance;
using BoltFramePlugin.Services;

namespace BoltFramePlugin.Views
{
    public partial class OpeningLimitsControl : System.Windows.Controls.UserControl
    {
        private readonly IPluginConfigurationManager _pluginConfig;
        private readonly ILoggingService _logger;
        private ObservableCollection<TableDRow> _tableDData;
        private ObservableCollection<TableERow> _tableEData;

        public OpeningLimitsControl()
        {
            InitializeComponent();

            _pluginConfig = DIContainerService.Container.GetInstance<IPluginConfigurationManager>();
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var config = _pluginConfig.LoadPluginConfiguration();

                // If no custom data exists, use defaults
                if (config.OpeningLimitsData == null)
                {
                    _tableDData = GetDefaultTableDData();
                    _tableEData = GetDefaultTableEData();
                }
                else
                {
                    _tableDData = new ObservableCollection<TableDRow>(config.OpeningLimitsData.TableD);
                    _tableEData = new ObservableCollection<TableERow>(config.OpeningLimitsData.TableE);
                }

                TableD.ItemsSource = _tableDData;
                TableE.ItemsSource = _tableEData;

                _logger.LogInformation("Opening Limits data loaded successfully");
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Error loading Opening Limits data", ex);
                // Fall back to defaults
                _tableDData = GetDefaultTableDData();
                _tableEData = GetDefaultTableEData();
                TableD.ItemsSource = _tableDData;
                TableE.ItemsSource = _tableEData;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = _pluginConfig.LoadPluginConfiguration();

                config.OpeningLimitsData = new OpeningLimitsTableData
                {
                    TableD = _tableDData.ToList(),
                    TableE = _tableEData.ToList()
                };

                _pluginConfig.SavePluginConfiguration(config);

                _logger.LogInformation("Opening Limits data saved successfully");
                System.Windows.MessageBox.Show("Opening limits data saved successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Error saving Opening Limits data", ex);
                System.Windows.MessageBox.Show($"Error saving opening limits data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to reset all table values to defaults?\nThis cannot be undone.",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _tableDData = GetDefaultTableDData();
                _tableEData = GetDefaultTableEData();
                TableD.ItemsSource = _tableDData;
                TableE.ItemsSource = _tableEData;

                _logger.LogInformation("Opening Limits data reset to defaults");
                System.Windows.MessageBox.Show("Table data has been reset to default values.", "Reset Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private ObservableCollection<TableDRow> GetDefaultTableDData()
        {
            return new ObservableCollection<TableDRow>
            {
                new TableDRow { MaxArea = "10", D0 = "0", D1_2 = "16", D1_5 = "24", D2_0 = "42", D2_5 = "66", D3 = "100", D4 = "", D5 = "", D6 = "", D7 = "", D8 = "", D9 = "" },
                new TableDRow { MaxArea = "15", D0 = "0", D1_2 = "16", D1_5 = "20", D2_0 = "34", D2_5 = "50", D3 = "74", D4 = "100", D5 = "", D6 = "", D7 = "", D8 = "", D9 = "" },
                new TableDRow { MaxArea = "20", D0 = "0", D1_2 = "16", D1_5 = "20", D2_0 = "30", D2_5 = "42", D3 = "60", D4 = "90", D5 = "100", D6 = "", D7 = "", D8 = "", D9 = "" },
                new TableDRow { MaxArea = "25", D0 = "0", D1_2 = "16", D1_5 = "18", D2_0 = "26", D2_5 = "38", D3 = "52", D4 = "90", D5 = "100", D6 = "", D7 = "", D8 = "", D9 = "" },
                new TableDRow { MaxArea = "30", D0 = "0", D1_2 = "14", D1_5 = "18", D2_0 = "24", D2_5 = "34", D3 = "46", D4 = "78", D5 = "100", D6 = "", D7 = "", D8 = "", D9 = "" },
                new TableDRow { MaxArea = "40", D0 = "0", D1_2 = "14", D1_5 = "16", D2_0 = "22", D2_5 = "30", D3 = "40", D4 = "64", D5 = "96", D6 = "100", D7 = "", D8 = "", D9 = "" },
                new TableDRow { MaxArea = "50", D0 = "0", D1_2 = "14", D1_5 = "16", D2_0 = "20", D2_5 = "28", D3 = "36", D4 = "56", D5 = "82", D6 = "100", D7 = "", D8 = "", D9 = "" },
                new TableDRow { MaxArea = "60", D0 = "0", D1_2 = "14", D1_5 = "16", D2_0 = "20", D2_5 = "26", D3 = "32", D4 = "50", D5 = "72", D6 = "98", D7 = "100", D8 = "", D9 = "" },
                new TableDRow { MaxArea = "80", D0 = "0", D1_2 = "14", D1_5 = "16", D2_0 = "18", D2_5 = "22", D3 = "28", D4 = "42", D5 = "58", D6 = "80", D7 = "100", D8 = "", D9 = "" },
                new TableDRow { MaxArea = "100", D0 = "0", D1_2 = "14", D1_5 = "16", D2_0 = "18", D2_5 = "22", D3 = "26", D4 = "36", D5 = "50", D6 = "68", D7 = "88", D8 = "100", D9 = "" },
                new TableDRow { MaxArea = "150 or more", D0 = "0", D1_2 = "14", D1_5 = "14", D2_0 = "16", D2_5 = "20", D3 = "22", D4 = "30", D5 = "40", D6 = "52", D7 = "66", D8 = "82", D9 = "100" }
            };

        }

        private ObservableCollection<TableERow> GetDefaultTableEData()
        {
            return new ObservableCollection<TableERow>
            {
                new TableERow { MaxArea = "10", E0 = "0", E1_2 = "8", E1_5 = "12", E2_0 = "20", E2_5 = "34", E3 = "50", E4 = "96", E5 = "100", E6 = "", E7 = "", E8 = "", E9 = "", E10 = "", E11 = "", E12 = "", E13 = "", E14 = "", E15 = "" },
                new TableERow { MaxArea = "15", E0 = "0", E1_2 = "8", E1_5 = "10", E2_0 = "16", E2_5 = "26", E3 = "36", E4 = "68", E5 = "100", E6 = "", E7 = "", E8 = "", E9 = "", E10 = "", E11 = "", E12 = "", E13 = "", E14 = "", E15 = "" },
                new TableERow { MaxArea = "20", E0 = "0", E1_2 = "8", E1_5 = "10", E2_0 = "14", E2_5 = "22", E3 = "30", E4 = "54", E5 = "86", E6 = "100", E7 = "", E8 = "", E9 = "", E10 = "", E11 = "", E12 = "", E13 = "", E14 = "", E15 = "" },
                new TableERow { MaxArea = "25", E0 = "0", E1_2 = "8", E1_5 = "10", E2_0 = "14", E2_5 = "18", E3 = "26", E4 = "44", E5 = "70", E6 = "100", E7 = "", E8 = "", E9 = "", E10 = "", E11 = "", E12 = "", E13 = "", E14 = "", E15 = "" },
                new TableERow { MaxArea = "30", E0 = "0", E1_2 = "8", E1_5 = "8", E2_0 = "12", E2_5 = "18", E3 = "24", E4 = "40", E5 = "60", E6 = "88", E7 = "100", E8 = "", E9 = "", E10 = "", E11 = "", E12 = "", E13 = "", E14 = "", E15 = "" },
                new TableERow { MaxArea = "40", E0 = "0", E1_2 = "8", E1_5 = "8", E2_0 = "12", E2_5 = "16", E3 = "20", E4 = "32", E5 = "48", E6 = "68", E7 = "94", E8 = "100", E9 = "", E10 = "", E11 = "", E12 = "", E13 = "", E14 = "", E15 = "" },
                new TableERow { MaxArea = "50", E0 = "0", E1_2 = "8", E1_5 = "8", E2_0 = "10", E2_5 = "14", E3 = "18", E4 = "28", E5 = "40", E6 = "58", E7 = "76", E8 = "100", E9 = "", E10 = "", E11 = "", E12 = "", E13 = "", E14 = "", E15 = "" },
                new TableERow { MaxArea = "60", E0 = "0", E1_2 = "8", E1_5 = "8", E2_0 = "10", E2_5 = "12", E3 = "16", E4 = "24", E5 = "36", E6 = "50", E7 = "66", E8 = "86", E9 = "100", E10 = "", E11 = "", E12 = "", E13 = "", E14 = "", E15 = "" },
                new TableERow { MaxArea = "80", E0 = "0", E1_2 = "8", E1_5 = "8", E2_0 = "10", E2_5 = "12", E3 = "14", E4 = "20", E5 = "30", E6 = "40", E7 = "52", E8 = "66", E9 = "84", E10 = "100", E11 = "", E12 = "", E13 = "", E14 = "", E15 = "" },
                new TableERow { MaxArea = "100", E0 = "0", E1_2 = "8", E1_5 = "8", E2_0 = "8", E2_5 = "10", E3 = "12", E4 = "18", E5 = "26", E6 = "34", E7 = "44", E8 = "56", E9 = "70", E10 = "84", E11 = "100", E12 = "", E13 = "", E14 = "", E15 = "" },
                new TableERow { MaxArea = "150", E0 = "0", E1_2 = "8", E1_5 = "8", E2_0 = "8", E2_5 = "10", E3 = "12", E4 = "16", E5 = "20", E6 = "26", E7 = "32", E8 = "40", E9 = "50", E10 = "60", E11 = "72", E12 = "84", E13 = "98", E14 = "100", E15 = "" },
                new TableERow { MaxArea = "200 or more", E0 = "0", E1_2 = "8", E1_5 = "8", E2_0 = "8", E2_5 = "8", E3 = "10", E4 = "14", E5 = "18", E6 = "22", E7 = "28", E8 = "34", E9 = "42", E10 = "50", E11 = "60", E12 = "68", E13 = "80", E14 = "92", E15 = "100" }
            };
        }
    }
}
