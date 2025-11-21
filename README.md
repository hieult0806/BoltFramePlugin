# LoBIM Revit Plugin

LoBIM is a .NET 8 add-in for Autodesk Revit 2026 that focuses on day-to-day production workflows: NBC limiting-distance review, tabular data import, sheet/view automation, and built-in diagnostics. The ribbon name is `LoBIM - SAIT` and the default buttons are driven by feature flags.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![Revit](https://img.shields.io/badge/revit-2026-orange.svg)

## Features (defaults from `Resources/Config/FeatureFlags.json`)
- **NBC Review**: Select a property line, detect perimeter walls, and group exposing building faces. Create limiting-distance arrows and projections, generate filled region types, initialize project parameters/families, and produce NBC compliance summaries (unprotected openings %, FRR, construction and cladding requirements) from `NBCRequirements.json`.
- **Import Table**: Import CSV/Excel via ClosedXML, preview data, control headers/delimiters, scale factors, text height, grid/fill options, and render as detail items in a drafting view. Track files for auto-sync with a background watcher or manual re-sync per view.
- **Manage Sheets**: List all sheets with parameter-based filtering, drag-drop reordering, preview new numbers, and apply renumbering with prefix/start/increment/padding through an external event.
- **Clone Views**: Read linked Revit files, pick views to copy with optional name prefix and positioning mode, ensure source-tracking parameters exist, and open/highlight cloned views in the Project Browser.
- **Clone Sheets**: Copy sheets (titleblocks + parameters) from linked files, search/select, re-clone to refresh content from the link, and open/highlight cloned sheets in the Project Browser.
- **Diagnostics**: Serilog writes to `%APPDATA%\Revit\LoBIM\LoBIM.log` (rolling). A ribbon command opens the log folder and a dockable pane surfaces live log messages.
- **Feature toggles**: BoltFrame and the Switch View panel are present but disabled by default; set `Enabled` to `true` in `FeatureFlags.json` if you need to test them.

## Prerequisites
- Autodesk Revit 2026 (API references resolve to `C:\Program Files\Autodesk\Revit 2026\`).
- .NET 8.0 SDK and Visual Studio 2022 (Windows x64).
- Windows 10/11.

## Build and run
1. `dotnet build LoBIM.sln -c Release` (prebuild kills any running Revit.exe; adjust `prebuild.bat` if undesired).
2. Post-build script (`post-build.ps1`) launches Revit with a sample file path - update it for your machine or comment it out.
3. Build output: `LoBIM/bin/<Config>/net8.0-windows/` with `LoBIM.dll` and the copied `Resources` folder.

## Install into Revit
1. Copy `LoBIM.addin` to `%APPDATA%\Autodesk\Revit\Addins\2026` and point the `<Assembly>` path at your built `LoBIM.dll`.
2. Place `LoBIM.dll` and the `Resources` folder (images, configs, families) alongside the manifest.
3. Start Revit and open the `LoBIM - SAIT` tab; only features with `Enabled: true` appear as ribbon buttons.

## Configuration and storage
- **Feature flags**: `LoBIM/Resources/Config/FeatureFlags.json` controls which buttons are shown.
- **NBC datasets**: `Resources/Config/NBCRequirements.json`, `FilledRegionTypes.json`, and `ProjectParameters.json` plus shipped families under `Resources/Families/`.
- **Plugin defaults**: `%APPDATA%\Revit\LoBIM\config.json` (working folder for project configs, logging toggle, default building classification, ray length limit, UI theme, recent projects, auto-arrow preference).
- **Project state**: `%APPDATA%\Revit\LoBIM\Projects\project-<guid>.json` persists per-project settings such as data-import options and tracked files.
- **Logging**: Daily rolling log at `%APPDATA%\Revit\LoBIM\LoBIM.log`; use the Open Logs button or the debug dockable pane to view entries.

## Notes
- Dependency highlights: SimpleInjector (DI), Serilog (logging), ClosedXML (Excel), Newtonsoft.Json/System.Text.Json (config), WPF/WinForms UI on .NET 8, Revit API 2026.
- BoltFrame structural framing UI remains available in source but is disabled in the default feature set.

## License

MIT license; see `LICENSE`.
