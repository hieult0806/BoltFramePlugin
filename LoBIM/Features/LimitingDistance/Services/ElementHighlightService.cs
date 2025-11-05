using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Services;

namespace LoBIM.Features.LimitingDistance.Services
{
    /// <summary>
    /// Service for highlighting elements in Revit with enhanced visual effects
    /// </summary>
    public class ElementHighlightService : IElementHighlightService
    {
        private readonly ILoggingService _logger;
        private readonly Dictionary<string, HashSet<ElementId>> _highlightedElements = new Dictionary<string, HashSet<ElementId>>();

        public ElementHighlightService(ILoggingService logger)
        {
            _logger = logger;
        }

        public void HighlightElement(UIDocument uidoc, ElementId elementId, bool zoomToElement = true, bool useColorOverride = true, Autodesk.Revit.DB.Color overrideColor = null)
        {
            if (uidoc?.ActiveView == null)
            {
                _logger.LogWarning("Cannot highlight element: No active view");
                return;
            }

            HighlightElementInView(uidoc, elementId, uidoc.ActiveView, zoomToElement, useColorOverride, overrideColor);
        }

        public void HighlightElementInView(UIDocument uidoc, ElementId elementId, Autodesk.Revit.DB.View targetView, bool zoomToElement = true, bool useColorOverride = true, Autodesk.Revit.DB.Color overrideColor = null)
        {
            if (uidoc == null || elementId == null || targetView == null)
            {
                _logger.LogWarning("Cannot highlight element: Invalid parameters");
                return;
            }

            var doc = uidoc.Document;

            try
            {
                using (var trans = new Transaction(doc, "Highlight Element"))
                {
                    trans.Start();

                    // 1. Clear previous highlights in this view
                    ClearHighlightsInView(doc, targetView);

                    // 2. Select the element
                    uidoc.Selection.SetElementIds(new List<ElementId> { elementId });

                    // 3. Apply color override if requested
                    if (useColorOverride)
                    {
                        var highlightColor = overrideColor ?? new Autodesk.Revit.DB.Color(0, 255, 255); // Default: Cyan
                        ApplyColorOverride(doc, targetView, elementId, highlightColor);

                        // Track highlighted element
                        var viewId = targetView.Id.ToString();
                        if (!_highlightedElements.ContainsKey(viewId))
                        {
                            _highlightedElements[viewId] = new HashSet<ElementId>();
                        }
                        _highlightedElements[viewId].Add(elementId);
                    }

                    trans.Commit();
                }

                // 4. Zoom to element if requested (must be after transaction)
                if (zoomToElement)
                {
                    ZoomToElement(uidoc, elementId);
                }

                _logger.LogInformation($"Highlighted element {elementId.Value} in view {targetView.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error highlighting element {elementId.Value}: {ex.Message}", ex);
            }
        }

        public void ClearHighlights(UIDocument uidoc)
        {
            if (uidoc?.ActiveView == null) return;

            ClearHighlightsInView(uidoc.Document, uidoc.ActiveView);
        }

        public void ClearElementHighlight(UIDocument uidoc, ElementId elementId)
        {
            if (uidoc?.ActiveView == null || elementId == null) return;

            var doc = uidoc.Document;
            var view = uidoc.ActiveView;

            try
            {
                using (var trans = new Transaction(doc, "Clear Element Highlight"))
                {
                    trans.Start();

                    var overrideSettings = new OverrideGraphicSettings();
                    view.SetElementOverrides(elementId, overrideSettings);

                    // Remove from tracked elements
                    var viewId = view.Id.ToString();
                    if (_highlightedElements.ContainsKey(viewId))
                    {
                        _highlightedElements[viewId].Remove(elementId);
                    }

                    trans.Commit();
                }

                _logger.LogInformation($"Cleared highlight for element {elementId.Value}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error clearing element highlight: {ex.Message}", ex);
            }
        }

        private void ClearHighlightsInView(Document doc, Autodesk.Revit.DB.View view)
        {
            try
            {
                var viewId = view.Id.ToString();
                if (_highlightedElements.ContainsKey(viewId))
                {
                    var elementsToReset = new List<ElementId>(_highlightedElements[viewId]);

                    foreach (var elementId in elementsToReset)
                    {
                        try
                        {
                            var overrideSettings = new OverrideGraphicSettings();
                            view.SetElementOverrides(elementId, overrideSettings);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Could not reset override for element {elementId.Value}: {ex.Message}");
                        }
                    }

                    _highlightedElements[viewId].Clear();
                    _logger.LogInformation($"Cleared {elementsToReset.Count} highlights in view {view.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error clearing highlights in view: {ex.Message}", ex);
            }
        }

        private void ApplyColorOverride(Document doc, Autodesk.Revit.DB.View view, ElementId elementId, Autodesk.Revit.DB.Color color)
        {
            try
            {
                var overrideSettings = new OverrideGraphicSettings();

                // Set surface pattern color (solid fill)
                overrideSettings.SetSurfaceForegroundPatternColor(color);
                overrideSettings.SetSurfaceForegroundPatternVisible(true);

                // Set projection line color and weight
                overrideSettings.SetProjectionLineColor(color);
                overrideSettings.SetProjectionLineWeight(8); // Make lines thicker

                // Set cut line color and weight
                overrideSettings.SetCutLineColor(color);
                overrideSettings.SetCutLineWeight(8);

                // Apply transparency to make it more visible but not obscure
                overrideSettings.SetSurfaceTransparency(30); // 30% transparent

                view.SetElementOverrides(elementId, overrideSettings);

                _logger.LogInformation($"Applied color override to element {elementId.Value}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error applying color override: {ex.Message}", ex);
            }
        }

        private void ZoomToElement(UIDocument uidoc, ElementId elementId)
        {
            try
            {
                var element = uidoc.Document.GetElement(elementId);
                if (element == null)
                {
                    _logger.LogWarning($"Cannot zoom: Element {elementId.Value} not found");
                    return;
                }

                var bbox = element.get_BoundingBox(uidoc.ActiveView);
                if (bbox == null)
                {
                    // Try to get bounding box from any view
                    bbox = element.get_BoundingBox(null);
                }

                if (bbox != null)
                {
                    // Expand the bounding box slightly for better framing
                    var min = bbox.Min;
                    var max = bbox.Max;
                    var expansion = (max - min).GetLength() * 0.2; // Expand by 20%

                    var expandedMin = new XYZ(
                        min.X - expansion,
                        min.Y - expansion,
                        min.Z - expansion
                    );

                    var expandedMax = new XYZ(
                        max.X + expansion,
                        max.Y + expansion,
                        max.Z + expansion
                    );

                    var expandedBBox = new BoundingBoxXYZ
                    {
                        Min = expandedMin,
                        Max = expandedMax
                    };

                    // Zoom to the bounding box
                    uidoc.GetOpenUIViews().FirstOrDefault()?.ZoomAndCenterRectangle(expandedMin, expandedMax);

                    _logger.LogInformation($"Zoomed to element {elementId.Value}");
                }
                else
                {
                    _logger.LogWarning($"Cannot zoom: No bounding box for element {elementId.Value}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error zooming to element: {ex.Message}", ex);
            }
        }
    }
}
