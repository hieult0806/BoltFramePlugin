# Feature Flags Configuration

This document explains how to control which features appear in the LoBIM ribbon menu.

## Overview

The `FeatureFlags.json` file allows you to enable or disable features before building the plugin. Only enabled features will appear in the Revit ribbon.

## Configuration File Location

```
LoBIM/Resources/Config/FeatureFlags.json
```

## How to Use

1. **Before Building**: Open `FeatureFlags.json` in your text editor
2. **Enable/Disable Features**: Set `"Enabled": true` or `"Enabled": false` for each feature
3. **Build the Project**: Run `dotnet build` or build from Visual Studio
4. **Launch Revit**: The ribbon will only show enabled features

## Example Configuration

### Enable All Features (Default)
```json
{
  "Features": {
    "BoltFrame": {
      "Enabled": true,
      "Name": "Bolt Frame",
      ...
    },
    "NBCReview": {
      "Enabled": true,
      "Name": "NBC Review",
      ...
    }
  }
}
```

### Disable Features Under Development
```json
{
  "Features": {
    "BoltFrame": {
      "Enabled": true,
      "Name": "Bolt Frame",
      ...
    },
    "ViewCloning": {
      "Enabled": false,   // ← This feature won't appear in the ribbon
      "Name": "Clone Views",
      ...
    },
    "NBCReview": {
      "Enabled": false,   // ← This feature won't appear in the ribbon
      "Name": "NBC Review",
      ...
    }
  }
}
```

## Available Features

| Feature Key | Feature Name | Description |
|------------|--------------|-------------|
| `BoltFrame` | Bolt Frame | Structural framing automation |
| `SwitchViewPanel` | View Plans | Quick view switching panel |
| `NBCReview` | NBC Review | NBC compliance review and limiting distance calculations |
| `DataImport` | Import Table | Import CSV/Excel to drafting views |
| `SheetManagement` | Manage Sheets | Sheet organization and renumbering |
| `ViewCloning` | Clone Views | Clone views from linked files |
| `OpenLogs` | Open Logs | Open the log folder |

## Feature Configuration Properties

Each feature has the following properties:

- **Enabled**: `true` or `false` - Controls visibility in ribbon
- **Name**: Display text for the button
- **ButtonName**: Unique identifier for the button
- **ClassName**: Full class name of the command
- **Tooltip**: Short tooltip text
- **LongDescription**: Detailed description shown in extended tooltip
- **IconName**: Icon file name (without extension)

## Tips

1. **Development Workflow**: Disable incomplete features to keep the ribbon clean
2. **Production Builds**: Enable all stable features for release
3. **Testing**: Enable only the feature you're currently testing
4. **Custom Deployments**: Create different configuration files for different clients

## Notes

- Changes take effect after rebuilding the project
- The file is copied to the output directory during build
- If the file is missing or invalid, all features default to enabled
- Check the log file for feature loading information

## Troubleshooting

If features aren't appearing as expected:

1. Check that `FeatureFlags.json` is in the `bin/Debug/net8.0-windows/Resources/Config/` folder after building
2. Verify the JSON syntax is valid (use a JSON validator)
3. Check the log file at `%AppData%\Revit\LoBIM\LoBIM-{date}.log` for loading errors
4. Ensure you've rebuilt the project after making changes
