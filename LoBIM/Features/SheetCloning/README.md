# Sheet Cloning – Technical Notes

Fast reference for how linked sheets are discovered, cloned, tracked, and refreshed.

## Purpose and scope
- Clone sheets from linked Revit files into the host document, preserving titleblocks, parameters, viewports, and positioning.
- Attach source-tracking metadata (file/name/id) so existing clones can be detected, opened, highlighted, or re-cloned.
- Support refresh (“re-clone”) to delete and rebuild a previously cloned sheet while keeping numbering constraints safe.

## Entry points and UI flow
- Feature flag `SheetCloning` in `Resources/Config/FeatureFlags.json` targets `Commands/SheetCloningCommand`.
- Command opens `Views/SheetCloningWindow.xaml` bound to `ViewModels/SheetCloningWindowVM`.
- View model bootstraps DI services, ensures tracking parameters, loads linked files/sheets, and exposes commands:
  - Refresh links, search/filter sheets, select/deselect, clone, re-clone, open a cloned sheet, and highlight in Project Browser.
  - External events (`EventHandlers/*`): `CloneSheetsEventHandler` (cloning transaction), `OpenSheetEventHandler`, `HighlightSheetEventHandler`, and `SheetCloningFailurePreprocessor` for failure logging.

## Core services and data contracts
- Models
  - `LinkedSheetInfo`: wraps a linked `ViewSheet`, selection/clone state, sheet number/name, viewport count, and source-tracking (`LoBIM_SourceFile`, `LoBIM_SourceSheetName`, `LoBIM_SourceSheetId`).
- Helpers
  - `SheetSourceTrackingHelper`: ensures/creates tracking parameters on sheets and stores metadata after cloning.
- `Services/SheetCloningService`
  - Reads sheets from a linked document (skips templates/placeholder), orders by sheet number, and populates host-tracking metadata.
  - `CloneSheets` manages session-wide sheet-number reservations, view clone cache, and per-sheet cloning.
  - `CloneSingleSheet` pipeline:
    - Reserve a unique sheet number (conflict-safe with temp numbers and recovery) and create/obtain matching titleblock (with optional transfer skip during re-clone).
    - Create host sheet, set final number/name, copy parameters, and store source tracking.
    - Clone and place each viewport: relies on `IViewCloningService` (with a shared cache) and repositions viewports using positioning mode (defaults to `InternalOriginToInternalOrigin`).
    - Handles legend/schedule viewport duplication and crop region checks; logs diagnostics heavily.
  - Generates unique numbers via `GenerateUniqueSheetNumber` with session cache + host checks; supports re-clone flow that deletes the prior clone first.
- `Services/ISheetCloningService`: interface for discovery and cloning entry points.

## Event orchestration and transactions
- `CloneSheetsEventHandler` runs cloning on the Revit API thread. It:
  - Ensures view and sheet tracking parameters exist (sheet helper + `PlanViewCloningStrategy.EnsureSourceTrackingParameters`).
  - Clones each sheet in its own transaction (safer commits and rollback per sheet) while sharing sheet-number reservation and view clone cache.
  - Supports re-clone: deletes existing clone before rebuilding.
- Open/highlight events briefly activate the sheet to make it visible in the Project Browser.

## Positioning and coordination with view cloning
- Viewports are cloned through `IViewCloningService` to respect view templates, crop regions, and link transforms. A per-session cache prevents duplicate view cloning across multiple sheets.
- Positioning mode (default `ViewPositioningMode.InternalOriginToInternalOrigin`) is passed through to view cloning to keep viewport placement aligned with the linked model; other modes can be surfaced via the VM if needed.

## Diagnostics and UX affordances
- Extensive Serilog logging for numbering, titleblock transfer, viewport placement, and transaction outcomes.
- UI shows clone status and source-tracking details and allows open/highlight of existing clones without re-cloning.

## Extensibility tips
- To alter numbering rules, adjust `GenerateUniqueSheetNumber` and keep the reserve/rename/temp-step safeguards intact.
- To add more viewport handling (e.g., custom placement rules), extend the viewport copy logic in `SheetCloningService` and ensure the positioning mode is respected.
- Consider surfacing additional positioning modes or sheet filters via the VM; ensure parameter creation still occurs outside active transactions.
