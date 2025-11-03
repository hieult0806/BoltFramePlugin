using BoltFramePlugin.Models.Tables;
using BoltFramePlugin.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BoltFramePlugin.Features.DataImport.Services.Import
{
    /// <summary>
    /// Service for importing data from CSV files
    /// </summary>
    public class CsvImportService : IDataImportService
    {
        private readonly ILoggingService _logger;

        public CsvImportService(ILoggingService logger)
        {
            _logger = logger;
        }

        public bool SupportsFileType(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".csv" || extension == ".txt";
        }

        public async Task<ImportedTableData> ImportAsync(string filePath, ImportConfiguration config)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV file not found: {filePath}");
            }

            _logger.LogInformation($"Importing CSV file: {filePath}");

            var result = new ImportedTableData
            {
                SourceFilePath = filePath,
                ImportedAt = DateTime.Now
            };

            try
            {
                // Read all lines asynchronously
                var lines = await Task.Run(() => File.ReadAllLines(filePath));

                if (lines.Length == 0)
                {
                    _logger.LogWarning("CSV file is empty");
                    return result;
                }

                var dataLines = new List<string>(lines);

                // Extract headers if configured
                if (config.HasHeaders && dataLines.Count > 0)
                {
                    var headerLine = dataLines[0];
                    result.Headers = ParseCsvLine(headerLine, config.CsvDelimiter, config.TrimWhitespace);
                    dataLines.RemoveAt(0);
                }
                else
                {
                    // Generate default headers (Column1, Column2, etc.)
                    if (dataLines.Count > 0)
                    {
                        var firstLine = ParseCsvLine(dataLines[0], config.CsvDelimiter, config.TrimWhitespace);
                        for (int i = 0; i < firstLine.Count; i++)
                        {
                            result.Headers.Add($"Column{i + 1}");
                        }
                    }
                }

                // Parse data rows
                foreach (var line in dataLines)
                {
                    if (string.IsNullOrWhiteSpace(line) && config.SkipEmptyRows)
                        continue;

                    var cells = ParseCsvLine(line, config.CsvDelimiter, config.TrimWhitespace);

                    // Ensure row has same column count as headers
                    while (cells.Count < result.Headers.Count)
                        cells.Add(string.Empty);

                    // Truncate if row has more columns than headers
                    if (cells.Count > result.Headers.Count)
                        cells = cells.Take(result.Headers.Count).ToList();

                    result.Rows.Add(cells);
                }

                _logger.LogInformation($"Successfully imported {result.RowCount} rows with {result.ColumnCount} columns");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error importing CSV file: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Parse a CSV line handling quoted values
        /// </summary>
        private List<string> ParseCsvLine(string line, char delimiter, bool trim)
        {
            var cells = new List<string>();
            var currentCell = string.Empty;
            var insideQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // Check for escaped quote ("")
                    if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentCell += '"';
                        i++; // Skip next quote
                    }
                    else
                    {
                        insideQuotes = !insideQuotes;
                    }
                }
                else if (c == delimiter && !insideQuotes)
                {
                    // End of cell
                    cells.Add(trim ? currentCell.Trim() : currentCell);
                    currentCell = string.Empty;
                }
                else
                {
                    currentCell += c;
                }
            }

            // Add last cell
            cells.Add(trim ? currentCell.Trim() : currentCell);

            return cells;
        }
    }
}
