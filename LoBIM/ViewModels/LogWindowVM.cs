using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LoBIM.ViewModels
{
    public class LogWindowVM : INotifyPropertyChanged, IWindowViewModel, IDisposable
    {
        private string _logText = string.Empty;
        private bool _autoScroll = true;
        private string _filterLevel = "All";
        private string _regexFilter = string.Empty;
        private bool _caseSensitive = false;
        private readonly ObservableCollection<LogEntry> _logEntries = new ObservableCollection<LogEntry>();
        private bool _dialogResult;
        private readonly ConcurrentQueue<LogEntry> _pendingLogEntries = new ConcurrentQueue<LogEntry>();
        private readonly System.Threading.Timer _batchTimer;
        private readonly object _refreshLock = new object();
        private const int MaxLogEntries = 10000; // Limit to prevent memory issues
        private const int BatchIntervalMs = 100; // Process logs every 100ms

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

            // Initialize batch timer for processing logs
            _batchTimer = new System.Threading.Timer(ProcessPendingLogs, null, BatchIntervalMs, BatchIntervalMs);
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

        public string RegexFilter
        {
            get => _regexFilter;
            set
            {
                _regexFilter = value;
                OnPropertyChanged(nameof(RegexFilter));
                RefreshLogText();
            }
        }

        public bool CaseSensitive
        {
            get => _caseSensitive;
            set
            {
                _caseSensitive = value;
                OnPropertyChanged(nameof(CaseSensitive));
                RefreshLogText();
            }
        }

        public string StatusText => $"Total logs: {_logEntries.Count}";

        public ICommand ClearLogsCommand { get; }

        private void OnLogMessageReceived(object? sender, LogMessageEventArgs e)
        {
            // Queue the log entry instead of immediately processing it
            // This prevents blocking the logging thread
            _pendingLogEntries.Enqueue(new LogEntry
            {
                Timestamp = e.Timestamp,
                Level = e.Level,
                Message = e.Message
            });
        }

        private void ProcessPendingLogs(object? state)
        {
            if (_pendingLogEntries.IsEmpty)
                return;

            // Process logs in batches to avoid excessive UI updates
            var logsToProcess = new List<LogEntry>();
            while (logsToProcess.Count < 100 && _pendingLogEntries.TryDequeue(out var logEntry))
            {
                logsToProcess.Add(logEntry);
            }

            if (logsToProcess.Count == 0)
                return;

            // Update UI on dispatcher thread, but use BeginInvoke for async operation
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    lock (_refreshLock)
                    {
                        foreach (var log in logsToProcess)
                        {
                            _logEntries.Add(log);

                            // Trim old entries if we exceed the max
                            if (_logEntries.Count > MaxLogEntries)
                            {
                                _logEntries.RemoveAt(0);
                            }
                        }

                        RefreshLogText();
                        OnPropertyChanged(nameof(StatusText));
                    }
                }
                catch (Exception)
                {
                    // Silently handle any UI update errors
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void RefreshLogText()
        {
            var filteredLogs = _logEntries.AsEnumerable();

            // Filter by log level
            if (FilterLevel != "All")
            {
                filteredLogs = filteredLogs.Where(l => l.Level == FilterLevel);
            }

            // Filter by regex pattern
            if (!string.IsNullOrEmpty(RegexFilter))
            {
                try
                {
                    var regexOptions = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    var regex = new Regex(RegexFilter, regexOptions);
                    filteredLogs = filteredLogs.Where(l => regex.IsMatch(l.Message));
                }
                catch (ArgumentException)
                {
                    // Invalid regex pattern, skip regex filtering
                }
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

        public void Dispose()
        {
            _batchTimer?.Dispose();
            LogSink.LogMessageReceived -= OnLogMessageReceived;
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
