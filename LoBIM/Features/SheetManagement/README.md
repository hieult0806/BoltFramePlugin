# Sheet Management – Technical Notes

Quick guide to how sheet listing, filtering, reordering, and renumbering are implemented.

## Purpose and scope
- List all sheets in the current document with parameter-based filtering and manual ordering controls.
- Preview and apply bulk renumbering using prefix/start/increment/padding while avoiding conflicts.
- Provide lightweight navigation helpers (jump to position) and status feedback for large sheet sets.

## Entry points and UI flow
- Feature flag `SheetManagement` in `Resources/Config/FeatureFlags.json` targets `Commands/SheetManagementCommand`.
- Command opens `Views/SheetManagementWindow.xaml` bound to `ViewModels/SheetManagementWindowVM`.
- View model sets defaults (e.g., prefix `A0.`, start 1, increment 1, padding 2), loads sheets at startup, and exposes commands:
  - Load/refresh, apply/clear parameter filter, move sheet up/down, insert at position (via `JumpToPositionDialog`), preview renumbering, and execute renumbering.
  - Renumbering runs through `EventHandlers/RenumberSheetsEventHandler` to execute Revit transactions safely.

## Core services and data contracts
- Models
  - `SheetItemModel`: sheet id, number, name, display order, originals for change tracking, and a dictionary of parameter name/value pairs for UI filtering.
  - `SheetRenumberingModel`: carries the renumber batch (sheets + prefix/start/increment/padding) and is used for preview/execute.
- Services
  - `Services/SheetManagementService`: retrieves/filters sheets, exposes available parameter names, checks uniqueness, and performs renumbering.
    - `GetAllSheets` collects non-template/non-placeholder sheets, builds `SheetItemModel` (including parameter snapshots).
    - `RenumberSheets` uses a two-pass transaction: assigns temporary numbers to break conflicts, then applies the desired numbers.
    - `PreviewRenumbering` (used by the VM) computes proposed numbers without touching the document so the UI can show a preview.
    - `IsSheetNumberUnique` / `GetSheetById` support validation and UI interactions.

## UI behavior (ViewModel)
- Filtering: user selects a parameter name and types filter text; the VM filters `SheetItemModel.Parameters` for matches.
- Ordering: move up/down adjusts `DisplayOrder`; insert uses `JumpToPositionDialog` to set a target ordinal.
- Renumbering: user sets prefix/start/increment/padding, previews, then executes; after execution, the VM refreshes the sheet list and status messages.
- Validation: renumber commands are enabled only when at least one sheet is selected and input values are valid; uniqueness checks happen before commit.

## Transactions and safety
- Renumbering runs inside `RenumberSheetsEventHandler` on the Revit API thread. Conflict avoidance uses temporary numbers and rolls back on errors.
- Logging through `ILoggingService` captures parameter read failures, uniqueness warnings, and transaction results.

## Extensibility tips
- To add new filters (e.g., regex, multi-parameter), extend the VM filter logic that inspects `SheetItemModel.Parameters`.
- To change numbering rules (e.g., per-discipline grouping), adapt `PreviewRenumbering` and `RenumberSheets` together to keep preview and execution consistent.
- If you introduce drag-drop ordering, reuse `DisplayOrder` from `SheetItemModel` and ensure the renumber batch keeps that order.
