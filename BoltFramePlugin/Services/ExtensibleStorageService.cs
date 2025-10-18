using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace BoltFramePlugin.Services
{
    /// <summary>
    /// Service for storing and retrieving plugin data using Revit's Extensible Storage API.
    /// Data is stored directly in the .rvt file and travels with the document.
    /// </summary>
    public class ExtensibleStorageService : IExtensibleStorageService
    {
        private readonly ILoggingService _logger;

        // Schema GUIDs - these should never change once published
        private static readonly Guid LimitingDistanceSchemaGuid = new Guid("B0A7F3E2-4D5C-4E8F-9A1B-2C3D4E5F6A7B");
        private const string LimitingDistanceSchemaName = "BoltFramePlugin_LimitingDistanceData";
        private const string StorageElementName = "BoltFramePlugin_LimitingDistance_Storage";

        public ExtensibleStorageService(ILoggingService logger)
        {
            _logger = logger;
        }

        #region Schema Creation

        /// <summary>
        /// Gets or creates the schema for limiting distance data
        /// </summary>
        private Schema GetOrCreateLimitingDistanceSchema()
        {
            // Try to lookup existing schema
            Schema schema = Schema.Lookup(LimitingDistanceSchemaGuid);

            if (schema != null)
            {
                _logger.LogInformation("Extensible Storage schema found.");
                return schema;
            }

            // Create new schema
            _logger.LogInformation("Creating new Extensible Storage schema...");

            SchemaBuilder schemaBuilder = new SchemaBuilder(LimitingDistanceSchemaGuid);
            schemaBuilder.SetSchemaName(LimitingDistanceSchemaName);
            schemaBuilder.SetReadAccessLevel(AccessLevel.Public);
            schemaBuilder.SetWriteAccessLevel(AccessLevel.Public);
            schemaBuilder.SetDocumentation("Stores limiting distance calculation data for BoltFramePlugin");

            // Add fields
            schemaBuilder.AddSimpleField("PropertyLineId", typeof(string))
                .SetDocumentation("ElementId of the selected property line");

            schemaBuilder.AddArrayField("PerimeterWallIds", typeof(string))
                .SetDocumentation("List of ElementIds of perimeter walls");

            schemaBuilder.AddSimpleField("BuildingClassification", typeof(string))
                .SetDocumentation("Selected building classification (e.g., Type IIA, Type IIB)");

            schemaBuilder.AddSimpleField("RayLengthLimit", typeof(double))
                .SetDocumentation("Ray casting length limit in feet");

            schemaBuilder.AddArrayField("ReferenceLineIds", typeof(string))
                .SetDocumentation("List of ElementIds of detected reference lines");

            schemaBuilder.AddArrayField("CreatedViewIds", typeof(string))
                .SetDocumentation("List of ElementIds of views created by the plugin");

            schemaBuilder.AddMapField("WallDistances", typeof(string), typeof(double))
                .SetDocumentation("Map of wall ElementId to calculated limiting distance");

            schemaBuilder.AddSimpleField("LastCalculationDate", typeof(string))
                .SetDocumentation("Timestamp of last calculation");

            schema = schemaBuilder.Finish();
            _logger.LogInformation("Extensible Storage schema created successfully.");

            return schema;
        }

        #endregion

        #region Storage Element Management

        /// <summary>
        /// Gets or creates the DataStorage element for storing plugin data
        /// </summary>
        private DataStorage GetOrCreateStorageElement(Document doc)
        {
            // Find existing storage element
            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(DataStorage))
                .Cast<DataStorage>();

            foreach (var storage in collector)
            {
                if (storage.Name == StorageElementName)
                {
                    _logger.LogInformation($"Found existing storage element: {storage.Id}");
                    return storage;
                }
            }

            // Create new storage element
            _logger.LogInformation("Creating new DataStorage element...");
            DataStorage newStorage = DataStorage.Create(doc);
            newStorage.Name = StorageElementName;

            return newStorage;
        }

        #endregion

        #region Save Data

        /// <summary>
        /// Saves limiting distance data to the document
        /// </summary>
        public void SaveLimitingDistanceData(
            Document doc,
            string propertyLineId,
            List<string> perimeterWallIds,
            string buildingClassification,
            double rayLengthLimit,
            List<string> referenceLineIds,
            List<string> createdViewIds,
            Dictionary<string, double> wallDistances)
        {
            try
            {
                Schema schema = GetOrCreateLimitingDistanceSchema();
                DataStorage storage = GetOrCreateStorageElement(doc);

                // Create entity
                Entity entity = new Entity(schema);

                // Set field values
                entity.Set("PropertyLineId", propertyLineId ?? string.Empty);
                entity.Set("PerimeterWallIds", perimeterWallIds ?? new List<string>());
                entity.Set("BuildingClassification", buildingClassification ?? string.Empty);
                entity.Set("RayLengthLimit", rayLengthLimit);
                entity.Set("ReferenceLineIds", referenceLineIds ?? new List<string>());
                entity.Set("CreatedViewIds", createdViewIds ?? new List<string>());

                // Convert dictionary to map
                if (wallDistances != null && wallDistances.Count > 0)
                {
                    var map = new Dictionary<string, double>(wallDistances);
                    entity.Set("WallDistances", map);
                }
                else
                {
                    entity.Set("WallDistances", new Dictionary<string, double>());
                }

                entity.Set("LastCalculationDate", DateTime.Now.ToString("o")); // ISO 8601 format

                // Save entity to storage
                storage.SetEntity(entity);

                _logger.LogInformation($"Saved limiting distance data to storage element {storage.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving limiting distance data: {ex.Message}", ex);
                throw;
            }
        }

        #endregion

        #region Load Data

        /// <summary>
        /// Loads limiting distance data from the document
        /// </summary>
        public LimitingDistanceStorageData LoadLimitingDistanceData(Document doc)
        {
            try
            {
                Schema schema = GetOrCreateLimitingDistanceSchema();

                // Find storage element
                var collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(DataStorage))
                    .Cast<DataStorage>();

                DataStorage storage = null;
                foreach (var ds in collector)
                {
                    if (ds.Name == StorageElementName)
                    {
                        storage = ds;
                        break;
                    }
                }

                if (storage == null)
                {
                    _logger.LogInformation("No storage element found, returning empty data.");
                    return new LimitingDistanceStorageData();
                }

                // Get entity
                Entity entity = storage.GetEntity(schema);

                if (!entity.IsValid())
                {
                    _logger.LogWarning("Storage element found but entity is invalid.");
                    return new LimitingDistanceStorageData();
                }

                // Read field values
                var data = new LimitingDistanceStorageData
                {
                    PropertyLineId = entity.Get<string>("PropertyLineId"),
                    PerimeterWallIds = entity.Get<IList<string>>("PerimeterWallIds")?.ToList() ?? new List<string>(),
                    BuildingClassification = entity.Get<string>("BuildingClassification"),
                    RayLengthLimit = entity.Get<double>("RayLengthLimit"),
                    ReferenceLineIds = entity.Get<IList<string>>("ReferenceLineIds")?.ToList() ?? new List<string>(),
                    CreatedViewIds = entity.Get<IList<string>>("CreatedViewIds")?.ToList() ?? new List<string>(),
                    WallDistances = entity.Get<IDictionary<string, double>>("WallDistances") as Dictionary<string, double>
                                   ?? new Dictionary<string, double>(),
                    LastCalculationDate = entity.Get<string>("LastCalculationDate")
                };

                _logger.LogInformation($"Loaded limiting distance data from storage element {storage.Id}");
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading limiting distance data: {ex.Message}", ex);
                return new LimitingDistanceStorageData();
            }
        }

        #endregion

        #region Clear Data

        /// <summary>
        /// Clears all stored limiting distance data from the document
        /// </summary>
        public void ClearLimitingDistanceData(Document doc)
        {
            try
            {
                // Find storage element
                var collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(DataStorage))
                    .Cast<DataStorage>();

                DataStorage storage = null;
                foreach (var ds in collector)
                {
                    if (ds.Name == StorageElementName)
                    {
                        storage = ds;
                        break;
                    }
                }

                if (storage != null)
                {
                    doc.Delete(storage.Id);
                    _logger.LogInformation($"Deleted storage element {storage.Id}");
                }
                else
                {
                    _logger.LogInformation("No storage element found to delete.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error clearing limiting distance data: {ex.Message}", ex);
                throw;
            }
        }

        #endregion
    }

    #region Data Model

    /// <summary>
    /// Model for limiting distance data stored in extensible storage
    /// </summary>
    public class LimitingDistanceStorageData
    {
        public string PropertyLineId { get; set; } = string.Empty;
        public List<string> PerimeterWallIds { get; set; } = new List<string>();
        public string BuildingClassification { get; set; } = string.Empty;
        public double RayLengthLimit { get; set; }
        public List<string> ReferenceLineIds { get; set; } = new List<string>();
        public List<string> CreatedViewIds { get; set; } = new List<string>();
        public Dictionary<string, double> WallDistances { get; set; } = new Dictionary<string, double>();
        public string LastCalculationDate { get; set; } = string.Empty;
    }

    #endregion
}
