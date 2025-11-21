# View Cloning – Technical Notes

A quick map of how the View Cloning feature is wired, what problems it solves, and where to extend it.

## Purpose and scope
- Clone printable views from linked Revit files into the host document, with optional name prefixing and positioning modes.
- Preserve source metadata (file, view name, view id) on the cloned view so it can be rediscovered and opened/highlighted later.
- Transfer view templates when needed and keep crop regions, scales, and orientation consistent with the source.

## Entry points and UI flow
- Ribbon button is driven by `Resources/Config/FeatureFlags.json` (`ViewCloning` points at `LoBIM.Features.ViewCloning.Commands.ViewCloningCommand`).
- `ViewCloningCommand` opens `Views/ViewCloningWindow.xaml`, bound to `ViewModels/ViewCloningWindowVM`.
- View model boots dependency-injected services, ensures tracking parameters exist, loads linked files, and exposes commands:
  - Refresh links, select/deselect views, set name prefix, choose `ViewPositioningMode`, and trigger cloning.
  - `ExternalEvent` handlers (`EventHandlers/*`) run Revit API work: clone views (`CloneViewsEventHandler`), open a cloned view (`OpenViewEventHandler`), or briefly activate a view to highlight it in the project browser (`HighlightViewEventHandler`).

## Core services and data contracts
- Models
  - `LinkedFileInfo`: wraps a `RevitLinkInstance`, linked `Document`, file metadata, and its `LinkedViewInfo` list.
  - `LinkedViewInfo`: wraps the source view, selection state, clone state/id, level name, scale, and stored source-tracking metadata (`LoBIM_SourceFile`, `LoBIM_SourceView`, `LoBIM_SourceViewId`).
  - `ViewPositioningMode`: `InternalOriginToInternalOrigin` (default, applies link transform), `ProjectBasePointToProjectBasePoint`, `BySharedCoordinates`.
- `Services/ViewCloningService`
  - Discovers linked files, filters cloneable views (printable; skips templates, sheets, legends, schedules, internals).
  - Checks host views for existing clones via source-tracking parameters.
  - `CloneViews` wraps a transaction and ensures parameters exist before cloning.
  - `CloneView` handles callout-parent dependencies (recursively clones missing parents), caches per-session clones to avoid duplicates, then routes cloning to a strategy via `ViewCloningStrategyFactory`.
- `Services/ViewTemplateTransferService`
  - Ensures a source view’s template exists in the host, copies it if missing, and applies it to the clone (with duplicate-type handling).

## Strategy routing and responsibilities
- Factory: `Factories/ViewCloningStrategyFactory` selects by concrete view type (`ViewSection`, `ViewPlan`, `View3D`), not by `ViewType` enum.
- Base class: `Strategies/BaseViewCloningStrategy` supplies shared helpers (view naming/sanitization, scale copy, crop/annotation copy, section-box copy, link transform resolution, template application, and parameter creation).
- Concrete strategies:
  - `PlanViewCloningStrategy`: clones floor/ceiling/engineering/area plans and plan callouts. Matches/creates host levels by name/elevation, applies link transforms to crop regions, and rebuilds callout crop loops with tight bounding boxes.
  - `SectionViewCloningStrategy`: clones sections/elevations (and callouts). Computes transformed crop boxes, compensates for Revit API quirks (basis Z flip and normalized bounding boxes), restores source crop and scale after template application.
  - `View3DViewCloningStrategy`: clones isometric/perspective views, copies orientation (eye/forward/up through link transform), section boxes, display style, and crops (rectangular only).

## Transactions and parameter guarantees
- Tracking parameters are created outside cloning transactions (`PlanViewCloningStrategy.EnsureSourceTrackingParameters` is invoked from the VM and before `CloneViews`).
- Cloning runs inside a single transaction per batch (`CloneViews`), while template transfers and metadata writes occur inside that transaction.

## Positioning and coordinate handling
- Link transforms (`RevitLinkInstance.GetTransform()`) are always considered; `InternalOriginToInternalOrigin` applies the transform, while shared-coordinate modes bypass it.
- Callouts: parent view is cloned first if missing. Callout crop origins are treated as world coordinates; bounding boxes are transformed per positioning mode before reconstruction.
- Levels: plans require a host level; if name matches fail, elevation matching is attempted; as a last resort a new level is created (with parameter copy where possible).

## Diagnostics and UX affordances
- Extensive logging via `ILoggingService` (Serilog backend) across services and strategies to debug coordinate/crop issues.
- Clone cache key = `<linked filename>_<source view id>` to avoid duplicate cloning in a session.
- UI shows source tracking, clone status, and allows open/highlight of existing clones without re-cloning.

## Extensibility tips
- To support a new view type, implement `IViewCloningStrategy` (ideally derive from `BaseViewCloningStrategy`), add routing in `ViewCloningStrategyFactory`, and register any model metadata the UI needs.
- When altering positioning rules, keep BaseViewCloningStrategy’s crop/callout transform assumptions in mind; incorrect transforms will surface as shifted crop boxes or section markers.
- Maintain parameter creation outside active transactions; template application can overwrite crop/scale, so restore critical geometry after applying templates as the section strategy does.
