using Autodesk.Revit.DB;

namespace LoBIM.Features.NBCReview.Services
{
    /// <summary>
    /// Service for saving and loading NBC Review data to/from Revit project
    /// </summary>
    public interface INBCDataPersistenceService
    {
        /// <summary>
        /// Saves NBC Review data to the project
        /// </summary>
        void SaveData(Document doc, NBCReviewData data);

        /// <summary>
        /// Loads NBC Review data from the project
        /// </summary>
        NBCReviewData LoadData(Document doc);

        /// <summary>
        /// Clears saved NBC Review data from the project
        /// </summary>
        void ClearData(Document doc);

        /// <summary>
        /// Saves a specific setting to the project
        /// </summary>
        void SaveSetting(Document doc, string key, string value);

        /// <summary>
        /// Loads a specific setting from the project
        /// </summary>
        string LoadSetting(Document doc, string key, string defaultValue = "");
    }

    /// <summary>
    /// Data transfer object for NBC Review data
    /// </summary>
    public class NBCReviewData
    {
        public string SelectedPropertyLineId { get; set; }
        public string BuildingClassification { get; set; }
        public double RayLengthLimit { get; set; }
        public bool AutoCreateArrows { get; set; }
    }
}
