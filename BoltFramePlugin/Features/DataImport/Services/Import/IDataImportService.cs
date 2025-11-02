using BoltFramePlugin.Features.DataImport.Models;
using System.Threading.Tasks;

namespace BoltFramePlugin.Features.DataImport.Services.Import
{
    /// <summary>
    /// Interface for importing data from external sources
    /// </summary>
    public interface IDataImportService
    {
        /// <summary>
        /// Import data from a file
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="config">Import configuration</param>
        /// <returns>Imported table data</returns>
        Task<ImportedTableData> ImportAsync(string filePath, ImportConfiguration config);

        /// <summary>
        /// Check if the service supports the given file type
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>True if supported</returns>
        bool SupportsFileType(string filePath);
    }
}
