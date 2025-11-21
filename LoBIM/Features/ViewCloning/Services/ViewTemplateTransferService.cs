using Autodesk.Revit.DB;
using LoBIM.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LoBIM.Features.ViewCloning.Services
{
    /// <summary>
    /// Service for transferring View Templates from linked documents to the host document
    /// </summary>
    public class ViewTemplateTransferService : IViewTemplateTransferService
    {
        private readonly ILoggingService _logger;

        public ViewTemplateTransferService(ILoggingService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Ensures that the View Template used by the source view exists in the host document.
        /// If the template doesn't exist, it will be transferred from the linked document.
        /// </summary>
        public ElementId EnsureViewTemplateExists(Document hostDoc, Autodesk.Revit.DB.View sourceView, Document linkedDoc)
        {
            try
            {
                _logger.LogInformation($"=== ENSURING VIEW TEMPLATE EXISTS ===");
                _logger.LogInformation($"Source view: {sourceView.Name}");

                // Check if source view has a template applied
                var sourceTemplateId = sourceView.ViewTemplateId;
                if (sourceTemplateId == null || sourceTemplateId == ElementId.InvalidElementId)
                {
                    _logger.LogInformation($"Source view does not have a View Template applied");
                    return ElementId.InvalidElementId;
                }

                // Get the template from the linked document
                var sourceTemplate = linkedDoc.GetElement(sourceTemplateId) as Autodesk.Revit.DB.View;
                if (sourceTemplate == null || !sourceTemplate.IsTemplate)
                {
                    _logger.LogWarning($"Could not get View Template from linked document (ID: {sourceTemplateId.Value})");
                    return ElementId.InvalidElementId;
                }

                _logger.LogInformation($"Source view uses template: '{sourceTemplate.Name}' (ID: {sourceTemplateId.Value})");

                // Check if template already exists in host document
                var existingTemplateId = FindViewTemplateByName(hostDoc, sourceTemplate.Name);
                if (existingTemplateId != ElementId.InvalidElementId)
                {
                    _logger.LogInformation($"Template '{sourceTemplate.Name}' already exists in host document (ID: {existingTemplateId.Value})");
                    return existingTemplateId;
                }

                // Template doesn't exist, transfer it
                _logger.LogInformation($"Template '{sourceTemplate.Name}' does not exist in host document - transferring...");
                var transferredTemplateId = TransferViewTemplate(hostDoc, sourceTemplateId, linkedDoc);

                if (transferredTemplateId != ElementId.InvalidElementId)
                {
                    _logger.LogInformation($"Successfully transferred template '{sourceTemplate.Name}' to host document (New ID: {transferredTemplateId.Value})");
                }
                else
                {
                    _logger.LogWarning($"Failed to transfer template '{sourceTemplate.Name}'");
                }

                return transferredTemplateId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error ensuring View Template exists: {ex.Message}", ex);
                return ElementId.InvalidElementId;
            }
        }

        /// <summary>
        /// Checks if a View Template with the given name exists in the document
        /// </summary>
        public ElementId FindViewTemplateByName(Document doc, string templateName)
        {
            try
            {
                var templates = new FilteredElementCollector(doc)
                    .OfClass(typeof(Autodesk.Revit.DB.View))
                    .Cast<Autodesk.Revit.DB.View>()
                    .Where(v => v.IsTemplate && v.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (templates.Any())
                {
                    var template = templates.First();
                    _logger.LogInformation($"Found existing template '{templateName}' in document (ID: {template.Id.Value})");
                    return template.Id;
                }

                _logger.LogInformation($"Template '{templateName}' not found in document");
                return ElementId.InvalidElementId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error finding View Template by name: {ex.Message}", ex);
                return ElementId.InvalidElementId;
            }
        }

        /// <summary>
        /// Transfers a View Template from the linked document to the host document
        /// Uses ElementTransformUtils.CopyElements to copy the template and its settings
        /// </summary>
        public ElementId TransferViewTemplate(Document hostDoc, ElementId templateId, Document linkedDoc)
        {
            try
            {
                _logger.LogInformation($"=== TRANSFERRING VIEW TEMPLATE ===");
                _logger.LogInformation($"Template ID in linked doc: {templateId.Value}");

                var sourceTemplate = linkedDoc.GetElement(templateId) as Autodesk.Revit.DB.View;
                if (sourceTemplate == null || !sourceTemplate.IsTemplate)
                {
                    _logger.LogWarning($"Element {templateId.Value} is not a valid View Template");
                    return ElementId.InvalidElementId;
                }

                _logger.LogInformation($"Transferring template: '{sourceTemplate.Name}' (Type: {sourceTemplate.ViewType})");

                // Create a list of element IDs to copy
                var elementsToCopy = new List<ElementId> { templateId };

                // Create copy/paste options to handle duplicate type names
                var copyOptions = new CopyPasteOptions();
                copyOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeNamesHandler());

                // Copy the template from linked document to host document
                // This will copy the template and all its settings
                var copiedIds = ElementTransformUtils.CopyElements(
                    linkedDoc,
                    elementsToCopy,
                    hostDoc,
                    Transform.Identity,
                    copyOptions
                );

                if (copiedIds != null && copiedIds.Count > 0)
                {
                    var copiedTemplateId = copiedIds.First();
                    var copiedTemplate = hostDoc.GetElement(copiedTemplateId) as Autodesk.Revit.DB.View;

                    if (copiedTemplate != null && copiedTemplate.IsTemplate)
                    {
                        _logger.LogInformation($"Successfully transferred template '{copiedTemplate.Name}' (New ID: {copiedTemplateId.Value})");
                        return copiedTemplateId;
                    }
                    else
                    {
                        _logger.LogWarning($"Copied element is not a valid View Template");
                        return ElementId.InvalidElementId;
                    }
                }
                else
                {
                    _logger.LogWarning($"CopyElements returned no elements");
                    return ElementId.InvalidElementId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error transferring View Template: {ex.Message}", ex);
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return ElementId.InvalidElementId;
            }
        }

        /// <summary>
        /// Applies a View Template to a target view if the source view has one
        /// This method must be called within an active transaction
        /// </summary>
        public bool ApplyViewTemplateIfExists(Document hostDoc, Autodesk.Revit.DB.View sourceView, Autodesk.Revit.DB.View targetView, Document linkedDoc)
        {
            try
            {
                _logger.LogInformation($"=== APPLYING VIEW TEMPLATE ===");
                _logger.LogInformation($"Source view: {sourceView.Name}");
                _logger.LogInformation($"Target view: {targetView.Name}");

                // Ensure the template exists in the host document (transfer if needed)
                var hostTemplateId = EnsureViewTemplateExists(hostDoc, sourceView, linkedDoc);

                if (hostTemplateId == ElementId.InvalidElementId)
                {
                    _logger.LogInformation($"No template to apply or template transfer failed");
                    return false;
                }

                // Apply the template to the target view
                try
                {
                    // Check if view type supports templates
                    if (!targetView.CanBePrinted)
                    {
                        _logger.LogWarning($"Target view '{targetView.Name}' does not support templates");
                        return false;
                    }

                    // Apply the template
                    targetView.ViewTemplateId = hostTemplateId;
                    _logger.LogInformation($"Successfully applied template (ID: {hostTemplateId.Value}) to view '{targetView.Name}'");
                    return true;
                }
                catch (Exception applyEx)
                {
                    _logger.LogWarning($"Could not apply template to view: {applyEx.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ApplyViewTemplateIfExists: {ex.Message}", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Handler for duplicate type names when copying elements
    /// Automatically uses the destination document's types when duplicates are encountered
    /// </summary>
    internal class DuplicateTypeNamesHandler : IDuplicateTypeNamesHandler
    {
        public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
        {
            // Use the destination document's types when duplicates are found
            // This prevents creating duplicate types and maintains consistency
            return DuplicateTypeAction.UseDestinationTypes;
        }
    }
}
