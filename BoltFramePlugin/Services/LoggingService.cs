using Serilog;
using System.IO;

namespace BoltFramePlugin.Services
{
    public class LoggingService : ILoggingService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _logFilePath;

        public LoggingService()
        {
            // Define the log directory, e.g., Revit's AppData or plugin's directory
            string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Revit", "BoltFramePlugin");
            Directory.CreateDirectory(logDirectory); // Ensure the directory exists

            _logFilePath = Path.Combine(logDirectory, "BoltFramePlugin.log");

            // Configure Serilog
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    path: _logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}"
                )
                .CreateLogger();
        }

        public void LogInformation(string message)
        {
            _logger.Information(message);
            ViewModels.LogSink.AddLogMessage("Information", message);
        }

        public void LogWarning(string message)
        {
            _logger.Warning(message);
            ViewModels.LogSink.AddLogMessage("Warning", message);
        }

        public void LogError(string message, Exception ex = null)
        {
            var fullMessage = ex != null ? $"{message}: {ex.Message}" : message;
            if (ex != null)
            {
                _logger.Error(ex, message);
            }
            else
            {
                _logger.Error(message);
            }
            ViewModels.LogSink.AddLogMessage("Error", fullMessage);
        }

        public void LogDebug(string message)
        {
            _logger.Debug(message);
            ViewModels.LogSink.AddLogMessage("Debug", message);
        }

        public void Dispose()
        {
            Log.CloseAndFlush();
        }
    }
}
