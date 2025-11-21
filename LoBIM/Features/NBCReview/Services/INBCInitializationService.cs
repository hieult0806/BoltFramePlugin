using System.Collections.Generic;
using Autodesk.Revit.DB;
using LoBIM.Features.NBCReview.Models;

namespace LoBIM.Features.NBCReview.Services
{
    /// <summary>
    /// Service for initializing NBC Review project setup - parameters, families, filled regions
    /// </summary>
    public interface INBCInitializationService
    {
        /// <summary>
        /// Initializes project parameters from configuration
        /// </summary>
        void InitializeProjectParameters(Document doc);

        /// <summary>
        /// Loads required families into the project
        /// </summary>
        void LoadRequiredFamilies(Document doc, List<string> familyPaths);

        /// <summary>
        /// Initializes filled region types from configuration
        /// </summary>
        void InitializeFilledRegionTypes(Document doc, List<FilledRegionTypeDefinition> definitions);

        /// <summary>
        /// Cleans up duplicate filled region types
        /// </summary>
        void CleanupDuplicateFilledRegionTypes(Document doc);

        /// <summary>
        /// Performs complete initialization (parameters + families + filled regions)
        /// </summary>
        void InitializeAll(Document doc, List<string> familyPaths, List<FilledRegionTypeDefinition> filledRegionDefs);

        /// <summary>
        /// Loads project parameters configuration from JSON
        /// </summary>
        List<ProjectParameterDefinition> LoadProjectParametersFromJson();

        /// <summary>
        /// Loads family list from folder
        /// </summary>
        List<string> LoadFamilyListFromFolder(string folderPath);

        /// <summary>
        /// Loads filled region type definitions from JSON
        /// </summary>
        List<FilledRegionTypeDefinition> LoadFilledRegionTypesFromJson();
    }
}
