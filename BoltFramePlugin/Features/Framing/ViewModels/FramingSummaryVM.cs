using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.UI;
using BoltFramePlugin.Features.Framing.Models;
using BoltFramePlugin.Helpers;
using BoltFramePlugin.ViewModels;

namespace BoltFramePlugin.Features.Framing.ViewModels
{
    public class FramingSummaryVM : BaseViewModel
    {
        public ICommand CloseCommand { get; }

        private ObservableCollection<FramingSummaryItem> _items;
        public ObservableCollection<FramingSummaryItem> Items
        {
            get => _items;
            set
            {
                _items = value;
                OnPropertyChanged(nameof(Items));
            }
        }

        private string _summaryText;
        public string SummaryText
        {
            get => _summaryText;
            set
            {
                _summaryText = value;
                OnPropertyChanged(nameof(SummaryText));
            }
        }

        public FramingSummaryVM(UIDocument document, FramingSummaryModel summaryModel) : base(document)
        {
            CloseCommand = new RelayCommand(OnClose);

            Items = new ObservableCollection<FramingSummaryItem>(summaryModel.Items);

            // Calculate summary text
            int totalBeams = summaryModel.Items.Sum(i => i.Count);
            double totalLength = summaryModel.Items.Sum(i => i.TotalLength);

            string groupInfo = summaryModel.IsGrouped
                ? $" | Grouped as: '{summaryModel.GroupName}'"
                : "";

            SummaryText = $"Total: {totalBeams} beams, {totalLength:F2} ft total length{groupInfo}";
        }

        private void OnClose(object parameter)
        {
            DialogResult = true;
            OnRequestClose(System.EventArgs.Empty);
        }
    }
}
