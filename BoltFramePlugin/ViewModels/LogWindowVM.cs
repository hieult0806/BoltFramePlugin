using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace BoltFramePlugin.ViewModels
{
    public class LogWindowVM : INotifyPropertyChanged, IWindowViewModel
    {
        private string _logText = string.Empty;
        private bool _autoScroll = true;
        private string _filterLevel = "All";
        private readonly ObservableCollection<LogEntry> _logEntries = new ObservableCollection<LogEntry>();
        private bool _dialogResult;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? RequestClose;

        public bool DialogResult
        {
            get => _dialogResult;
            set
            {
                _dialogResult = value;
                OnPropertyChanged(nameof(DialogResult));
            }
        }

        public LogWindowVM()
        {
            ClearLogsCommand = new RelayCommand(_ => ClearLogs());

            // Subscribe to the log sink
            LogSink.LogMessageReceived += OnLogMessageReceived;
        }

        public string LogText
        {
            get => _logText;
            set
            {
                _logText = value;
                OnPropertyChanged(nameof(LogText));
            }
        }

        public bool AutoScroll
        {
            get => _autoScroll;
            set
            {
                _autoScroll = value;
                OnPropertyChanged(nameof(AutoScroll));
            }
        }

        public string FilterLevel
        {
            get => _filterLevel;
            set
            {
                _filterLevel = value;
                OnPropertyChanged(nameof(FilterLevel));
                RefreshLogText();
            }
        }

        public string StatusText => $"Total logs: {_logEntries.Count}";

        public ICommand ClearLogsCommand { get; }

        private void OnLogMessageReceived(object? sender, LogMessageEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _logEntries.Add(new LogEntry
                {
                    Timestamp = e.Timestamp,
                    Level = e.Level,
                    Message = e.Message
                });

                RefreshLogText();
                OnPropertyChanged(nameof(StatusText));
            });
        }

        private void RefreshLogText()
        {
            var filteredLogs = _logEntries.AsEnumerable();

            if (FilterLevel != "All")
            {
                filteredLogs = filteredLogs.Where(l => l.Level == FilterLevel);
            }

            var sb = new StringBuilder();
            foreach (var log in filteredLogs)
            {
                sb.AppendLine($"[{log.Timestamp:HH:mm:ss.fff}] [{log.Level}] {log.Message}");
            }

            LogText = sb.ToString();
        }

        private void ClearLogs()
        {
            _logEntries.Clear();
            LogText = string.Empty;
            OnPropertyChanged(nameof(StatusText));
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class LogMessageEventArgs : EventArgs
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public static class LogSink
    {
        public static event EventHandler<LogMessageEventArgs>? LogMessageReceived;

        public static void AddLogMessage(string level, string message)
        {
            LogMessageReceived?.Invoke(null, new LogMessageEventArgs
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            });
        }
    }
}
