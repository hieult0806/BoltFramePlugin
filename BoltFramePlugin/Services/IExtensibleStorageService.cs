using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace BoltFramePlugin.Services
{
    /// <summary>
    /// Interface for storing and retrieving plugin data using Revit's Extensible Storage API
    /// </summary>
    public interface IExtensibleStorageService
    {
        /// <summary>
        /// Saves limiting distance data to the document's extensible storage
        /// </summary>
        void SaveLimitingDistanceData(
            Document doc,
            string propertyLineId,
            List<string> perimeterWallIds,
            string buildingClassification,
            double rayLengthLimit,
            List<string> referenceLineIds,
            List<string> createdViewIds,
            Dictionary<string, double> wallDistances);

        /// <summary>
        /// Loads limiting distance data from the document's extensible storage
        /// </summary>
        LimitingDistanceStorageData LoadLimitingDistanceData(Document doc);

        /// <summary>
        /// Clears all stored limiting distance data from the document
        /// </summary>
        void ClearLimitingDistanceData(Document doc);
    }
}
