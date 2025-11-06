using Autodesk.Revit.DB;
using LoBIM.Features.ViewCloning.Strategies;
using LoBIM.Services;
using System;

namespace LoBIM.Features.ViewCloning.Factories
{
    /// <summary>
    /// Factory for creating appropriate view cloning strategies based on concrete view type
    /// Routes by view.GetType() (ViewSection, ViewPlan, View3D) - NOT by view.ViewType enum
    /// Each strategy handles both regular views and callouts for its type
    /// </summary>
    public class ViewCloningStrategyFactory
    {
        private readonly SectionViewCloningStrategy _sectionStrategy;
        private readonly PlanViewCloningStrategy _planStrategy;
        private readonly View3DViewCloningStrategy _view3DStrategy;
        private readonly ILoggingService _logger;

        public ViewCloningStrategyFactory(ILoggingService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize strategies based on concrete view types
            // ViewSection handles: Section views, Elevation views (both regular and callouts)
            _sectionStrategy = new SectionViewCloningStrategy(logger);

            // ViewPlan handles: FloorPlan, CeilingPlan, EngineeringPlan (both regular and callouts)
            _planStrategy = new PlanViewCloningStrategy(logger);

            // View3D handles: 3D views
            _view3DStrategy = new View3DViewCloningStrategy(logger);
        }

        /// <summary>
        /// Gets the appropriate strategy for cloning a view based on its concrete type
        /// Routes by view.GetType() which returns ViewSection, ViewPlan, or View3D
        /// </summary>
        /// <param name="view">The view to clone</param>
        /// <returns>The strategy that can handle this view type, or null if not supported</returns>
        public IViewCloningStrategy GetStrategy(Autodesk.Revit.DB.View view)
        {
            if (view == null)
            {
                _logger.LogWarning($"Cannot get strategy for null view");
                return null;
            }

            // Route by concrete view type (not ViewType enum)
            string concreteType = view.GetType().Name;

            _logger.LogInformation($"=== VIEW ROUTING ===");
            _logger.LogInformation($"View Name: {view.Name}");
            _logger.LogInformation($"Concrete Type: {concreteType} (from view.GetType().Name)");
            _logger.LogInformation($"ViewType Enum: {view.ViewType}");
            _logger.LogInformation($"Is Callout: {view.IsCallout}");

            IViewCloningStrategy strategy = null;

            if (view is ViewSection)
            {
                // ViewSection covers: Section views AND Elevation views
                _logger.LogInformation($"Routing to SectionViewCloningStrategy (handles sections, elevations, and their callouts)");
                strategy = _sectionStrategy;
            }
            else if (view is ViewPlan)
            {
                // ViewPlan covers: FloorPlan, CeilingPlan, EngineeringPlan, AreaPlan
                _logger.LogInformation($"Routing to PlanViewCloningStrategy (handles all plan types and their callouts)");
                strategy = _planStrategy;
            }
            else if (view is View3D)
            {
                // View3D covers: 3D views (Isometric, Perspective)
                _logger.LogInformation($"Routing to View3DViewCloningStrategy (handles 3D views)");
                strategy = _view3DStrategy;
            }
            else
            {
                _logger.LogWarning($"Unsupported view concrete type: {concreteType}");
                strategy = null;
            }

            return strategy;
        }

        /// <summary>
        /// Gets all supported view types
        /// </summary>
        /// <returns>List of view types that have cloning strategies</returns>
        public IEnumerable<ViewType> GetSupportedViewTypes()
        {
            return new[]
            {
                ViewType.Section,
                ViewType.Elevation,
                ViewType.FloorPlan,
                ViewType.CeilingPlan,
                ViewType.EngineeringPlan,
                ViewType.Detail,
                ViewType.ThreeD
            };
        }
    }
}
