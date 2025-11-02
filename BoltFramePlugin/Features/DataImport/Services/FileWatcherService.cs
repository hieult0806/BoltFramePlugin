using Autodesk.Revit.DB;
using BoltFramePlugin.Features.DataImport.Models;
using BoltFramePlugin.Models;
using BoltFramePlugin.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TextAlignment = BoltFramePlugin.Features.DataImport.Models.TextAlignment;

namespace BoltFramePlugin.Features.DataImport.Services
{
    /// <summary>
    /// Service for tracking imported files and manually syncing changes to Revit views
    /// </summary>
    public class FileWatcherService : IDisposable
    {
        private readonly ILoggingService _logger;
        private readonly DataImportManager _importManager;
        private readonly IProjectConfigurationManager _projectConfig;
        private readonly Dictionary<string, WatchedFileInfo> _watchedFiles;
        private readonly object _lockObject = new object();
        private Document? _currentDocument;

        public FileWatcherService(ILoggingService logger, DataImportManager importManager, IProjectConfigurationManager projectConfig)
        {
            _logger = logger;
            _importManager = importManager;
            _projectConfig = projectConfig;
            _watchedFiles = new Dictionary<string, WatchedFileInfo>();

            _logger.LogInformation("FileWatcherService initialized for manual syncing");
        }

        /// <summary>
        /// Track an imported file for manual syncing
        /// </summary>
        public void TrackFile(string filePath, Document doc, string viewName, ImportConfiguration importConfig, TableRenderOptions renderOptions)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                _logger.LogWarning($"Cannot watch file - file does not exist: {filePath}");
                return;
            }

            string key = $"{filePath}:{viewName}";

            lock (_lockObject)
            {
                // Check if already watching this file for this view
                if (_watchedFiles.ContainsKey(key))
                {
                    _logger.LogInformation($"Already watching file: {filePath} for view: {viewName}");

                    // Check if OnSyncRequested has subscribers
                    int subscriberCount = OnSyncRequested?.GetInvocationList()?.Length ?? 0;
                    _logger.LogInformation($"OnSyncRequested has {subscriberCount} subscriber(s)");

                    return;
                }

                // Check if we're watching this file for a DIFFERENT view and stop that watcher
                var existingWatchersForFile = _watchedFiles.Keys
                    .Where(k => k.StartsWith($"{filePath}:"))
                    .ToList();

                foreach (var existingKey in existingWatchersForFile)
                {
                    _logger.LogInformation($"Removing existing watcher for file {filePath} (old key: {existingKey}, new key: {key})");
                    _watchedFiles.Remove(existingKey);
                }

                // Store watched file info
                var watchInfo = new WatchedFileInfo
                {
                    FilePath = filePath,
                    Document = doc,
                    ViewName = viewName,
                    ImportConfig = importConfig,
                    RenderOptions = renderOptions,
                    LastModified = File.GetLastWriteTime(filePath)
                };

                _watchedFiles[key] = watchInfo;

                _logger.LogInformation($"Started watching file: {filePath} for view: {viewName} (Key: {key}, LastModified: {watchInfo.LastModified})");
                _logger.LogInformation($"Total watched files: {_watchedFiles.Count}");

                // Store current document reference
                _currentDocument = doc;

                // Save to configuration
                SaveTrackedFilesToConfig();
            }
        }

        /// <summary>
        /// Load tracked files from project configuration for a specific document
        /// </summary>
        public void LoadTrackedFiles(Document doc)
        {
            if (doc == null || doc.ProjectInformation == null)
            {
                _logger.LogWarning("Cannot load tracked files - invalid document");
                return;
            }

            try
            {
                _currentDocument = doc;

                // Use ProjectInformation ElementId as a stable identifier
                var projectIdString = doc.ProjectInformation.UniqueId;
                Guid projectId;

                // Try to parse as GUID, if fails create a deterministic GUID from the string
                if (!Guid.TryParse(projectIdString, out projectId))
                {
                    // Create a deterministic GUID from the unique ID string
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(projectIdString));
                        projectId = new Guid(hash);
                    }
                }

                var config = _projectConfig.LoadProjectConfiguration(projectId);

                if (config.TrackedFiles == null || !config.TrackedFiles.Any())
                {
                    _logger.LogInformation("No tracked files found in configuration");
                    return;
                }

                lock (_lockObject)
                {
                    foreach (var trackedConfig in config.TrackedFiles)
                    {
                        string key = $"{trackedConfig.FilePath}:{trackedConfig.ViewName}";

                        // Convert TrackedFileConfig back to WatchedFileInfo
                        var watchInfo = new WatchedFileInfo
                        {
                            FilePath = trackedConfig.FilePath,
                            Document = doc,
                            ViewName = trackedConfig.ViewName,
                            LastModified = trackedConfig.LastModified,
                            ImportConfig = new ImportConfiguration
                            {
                                HasHeaders = trackedConfig.HasHeaders,
                                CsvDelimiter = trackedConfig.CsvDelimiter,
                                SkipEmptyRows = trackedConfig.SkipEmptyRows,
                                TrimWhitespace = trackedConfig.TrimWhitespace
                            },
                            RenderOptions = new TableRenderOptions
                            {
                                ColumnWidth = trackedConfig.ColumnWidth,
                                RowHeight = trackedConfig.RowHeight,
                                TextHeight = trackedConfig.TextHeight,
                                ViewScale = trackedConfig.ViewScale,
                                PaperTextHeight = trackedConfig.PaperTextHeight,
                                BorderOffset = trackedConfig.BorderOffset,
                                TextOffsetX = trackedConfig.TextOffsetX,
                                TextOffsetY = trackedConfig.TextOffsetY,
                                DrawGridLines = trackedConfig.DrawGridLines,
                                FillHeaderBackground = trackedConfig.FillHeaderBackground,
                                AutoSizeColumns = trackedConfig.AutoSizeColumns,
                                TextAlign = (TextAlignment)(int)trackedConfig.TextAlign
                            }
                        };

                        _watchedFiles[key] = watchInfo;
                        _logger.LogInformation($"Loaded tracked file from config: {trackedConfig.FilePath} -> {trackedConfig.ViewName}");
                    }

                    _logger.LogInformation($"Loaded {config.TrackedFiles.Count} tracked file(s) from configuration");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading tracked files: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Save currently tracked files to project configuration
        /// </summary>
        private void SaveTrackedFilesToConfig()
        {
            if (_currentDocument == null || _currentDocument.ProjectInformation == null)
            {
                _logger.LogWarning("Cannot save tracked files - no current document");
                return;
            }

            try
            {
                // Use ProjectInformation ElementId as a stable identifier
                var projectIdString = _currentDocument.ProjectInformation.UniqueId;
                Guid projectId;

                // Try to parse as GUID, if fails create a deterministic GUID from the string
                if (!Guid.TryParse(projectIdString, out projectId))
                {
                    // Create a deterministic GUID from the unique ID string
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(projectIdString));
                        projectId = new Guid(hash);
                    }
                }

                var config = _projectConfig.LoadProjectConfiguration(projectId);

                // Convert current watched files to TrackedFileConfig
                config.TrackedFiles = _watchedFiles.Values.Select(w => new TrackedFileConfig
                {
                    FilePath = w.FilePath,
                    ViewName = w.ViewName,
                    LastModified = w.LastModified,
                    HasHeaders = w.ImportConfig.HasHeaders,
                    CsvDelimiter = w.ImportConfig.CsvDelimiter,
                    SkipEmptyRows = w.ImportConfig.SkipEmptyRows,
                    TrimWhitespace = w.ImportConfig.TrimWhitespace,
                    ColumnWidth = w.RenderOptions.ColumnWidth,
                    RowHeight = w.RenderOptions.RowHeight,
                    TextHeight = w.RenderOptions.TextHeight,
                    ViewScale = (int)w.RenderOptions.ViewScale,
                    PaperTextHeight = w.RenderOptions.PaperTextHeight,
                    BorderOffset = w.RenderOptions.BorderOffset,
                    TextOffsetX = w.RenderOptions.TextOffsetX,
                    TextOffsetY = w.RenderOptions.TextOffsetY,
                    DrawGridLines = w.RenderOptions.DrawGridLines,
                    FillHeaderBackground = w.RenderOptions.FillHeaderBackground,
                    AutoSizeColumns = w.RenderOptions.AutoSizeColumns,
                    TextAlign = (int)w.RenderOptions.TextAlign
                }).ToList();

                _projectConfig.SaveProjectConfiguration(config);
                _logger.LogInformation($"Saved {config.TrackedFiles.Count} tracked file(s) to configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving tracked files: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Stop watching a specific file for a specific view
        /// </summary>
        public void StopWatching(string filePath, string viewName)
        {
            string key = $"{filePath}:{viewName}";

            lock (_lockObject)
            {
                if (_watchedFiles.Remove(key))
                {
                    _logger.LogInformation($"Stopped watching file: {filePath} for view: {viewName}");
                    SaveTrackedFilesToConfig();
                }
            }
        }

        /// <summary>
        /// Stop watching all files
        /// </summary>
        public void StopAll()
        {
            lock (_lockObject)
            {
                int count = _watchedFiles.Count;
                _watchedFiles.Clear();
                _logger.LogInformation($"Stopped watching all files ({count} file(s))");
            }
        }

        /// <summary>
        /// Get list of currently watched files
        /// </summary>
        public List<WatchedFileInfo> GetWatchedFiles()
        {
            lock (_lockObject)
            {
                return _watchedFiles.Values.ToList();
            }
        }

        /// <summary>
        /// Manually sync a specific tracked file
        /// </summary>
        public void SyncFile(string filePath, string viewName)
        {
            string key = $"{filePath}:{viewName}";

            lock (_lockObject)
            {
                if (!_watchedFiles.TryGetValue(key, out var watchInfo))
                {
                    _logger.LogWarning($"File not being tracked: {filePath} for view: {viewName}");
                    return;
                }

                _logger.LogInformation($"Manual sync requested for: {filePath} -> {viewName}");

                if (!File.Exists(watchInfo.FilePath))
                {
                    _logger.LogError($"File no longer exists: {watchInfo.FilePath}");
                    return;
                }

                // Update last modified time
                watchInfo.LastModified = File.GetLastWriteTime(watchInfo.FilePath);

                // Trigger sync
                OnSyncRequested?.Invoke(this, new SyncRequestedEventArgs
                {
                    FilePath = watchInfo.FilePath,
                    Document = watchInfo.Document,
                    ViewName = watchInfo.ViewName,
                    ImportConfig = watchInfo.ImportConfig,
                    RenderOptions = watchInfo.RenderOptions
                });
            }
        }

        /// <summary>
        /// Check if file has been modified since last sync
        /// </summary>
        public bool HasFileChanged(string filePath, string viewName)
        {
            string key = $"{filePath}:{viewName}";

            lock (_lockObject)
            {
                if (!_watchedFiles.TryGetValue(key, out var watchInfo))
                {
                    return false;
                }

                if (!File.Exists(watchInfo.FilePath))
                {
                    return false;
                }

                var currentModified = File.GetLastWriteTime(watchInfo.FilePath);
                return currentModified > watchInfo.LastModified;
            }
        }

        public event EventHandler<SyncRequestedEventArgs>? OnSyncRequested;

        public void Dispose()
        {
            StopAll();
            _logger.LogInformation("FileWatcherService disposed");
        }
    }

    public class WatchedFileInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public Document? Document { get; set; }
        public string ViewName { get; set; } = string.Empty;
        public ImportConfiguration ImportConfig { get; set; } = new ImportConfiguration();
        public TableRenderOptions RenderOptions { get; set; } = new TableRenderOptions();
        public DateTime LastModified { get; set; }
    }

    public class SyncRequestedEventArgs : EventArgs
    {
        public string FilePath { get; set; } = string.Empty;
        public Document? Document { get; set; }
        public string ViewName { get; set; } = string.Empty;
        public ImportConfiguration ImportConfig { get; set; } = new ImportConfiguration();
        public TableRenderOptions RenderOptions { get; set; } = new TableRenderOptions();
    }
}
