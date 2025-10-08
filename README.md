# BoltFrame Plugin - Autodesk Revit 3D Structural Framing Automation

A professional-grade Autodesk Revit 2025 plugin that automates 3D structural framing design through intelligent geometry analysis and procedural generation. This plugin demonstrates advanced 3D programming, computational geometry, and CAD automation expertise.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![Revit](https://img.shields.io/badge/Revit-2025-orange.svg)

---

## 🎯 Project Overview

**BoltFrame Plugin** is a 3D construction automation tool that analyzes building geometry and procedurally generates optimized structural framing systems. The plugin integrates directly into Autodesk Revit's 3D modeling environment, providing architects and engineers with intelligent automation for structural beam and column placement.

### Key Features

- **3D Geometry Analysis**: Automatically analyzes floor surfaces, edges, and topology to determine optimal framing layouts
- **Procedural 3D Modeling**: Generates complex 3D structural frameworks using vector mathematics and geometric algorithms
- **Interactive 3D Interface**: Custom WPF/XAML UI integrated into Revit's 3D viewport with real-time element preview
- **Multi-Floor Automation**: Batch processing of multiple building levels with configurable grid patterns
- **Intelligent Element Selection**: 3D ray-casting and selection filtering for interactive object manipulation

---

## 💼 Relevance to 3D Estimating Software Development

This project demonstrates direct experience with the core competencies required for developing **3D Estimating Software**:

### 3D Programming & Computational Geometry
- Implementation of **vector-based mathematics** for 3D object manipulation and placement
- **Linear algebra operations**: Cross products, normal calculations, vector projections
- **3D geometric algorithms**: Edge offsetting, curve tessellation, surface analysis
- **Spatial reasoning**: Bounding box calculations, containment tests, distance computations

### Object-Oriented Design Patterns
- **Strategy Pattern**: Flexible framing algorithms (`IFramingStrategy`) for different building elements
- **Factory Pattern**: Dynamic instantiation based on element geometry types
- **Dependency Injection**: Decoupled architecture using SimpleInjector container
- **MVVM Pattern**: Clean separation of 3D visualization logic and business logic

### 3D Interface Development
- Custom **3D viewport integration** within existing CAD environment
- **Dockable panels** and ribbon UI for 3D tool access
- **Real-time 3D preview** of elements before placement
- **Interactive 3D selection** with custom filtering and highlighting

### Construction & CAD Domain Knowledge
- Deep understanding of **structural framing systems** (beams, joists, columns)
- **Building level management** and vertical coordination
- **Construction documentation** standards and practices
- Integration with industry-standard **BIM (Building Information Modeling)** workflows

---

## 🔧 Technical Stack

### Core Technologies
- **Language**: C# 12.0
- **Framework**: .NET 8.0 (Windows)
- **IDE**: Visual Studio 2022
- **3D API**: Autodesk Revit API 2025
- **UI Frameworks**: WPF (XAML), Windows Forms

### Key Libraries & Patterns
- **Geometry Processing**: Revit API Geometry Classes (Face, Edge, Curve, XYZ vectors)
- **Dependency Injection**: SimpleInjector 5.5.0
- **Logging**: Serilog 4.0.2
- **Serialization**: Newtonsoft.Json 13.0.3

### Design Patterns Implemented
- Strategy Pattern (framing generation strategies)
- Factory Pattern (element creation)
- Observer Pattern (Revit event handling)
- Command Pattern (UI command binding)
- Repository Pattern (configuration management)

---

## 🏗️ Architecture Overview

### 3D Geometry Processing Pipeline

```
User Selection → 3D Element Analysis → Surface Extraction → Edge Detection
    ↓
Vector Mathematics (normals, tangents, offsets) → Grid Generation
    ↓
3D Beam Placement → Structural Type Assignment → Document Update
```

### Core Components

#### 1. **3D Framing Strategies** (`FramingStrategies/`)
Advanced geometric algorithms for automated 3D element generation:

```csharp
public interface IFramingStrategy
{
    IRevitService RevitService { get; set; }
    IFrameGenerateModel Model { get; set; }
    void StartGenerate();
}
```

- **`FloorFramingStrategy`**: Analyzes floor geometry to generate beam grids
  - Computes face normals and edge tangents
  - Creates offset curves using cross products
  - Generates orthogonal grid patterns via vector projections

- **`MultiFloorFramingStrategy`**: Batch processing across multiple levels
  - Configurable grid orientations (horizontal/vertical)
  - Multi-directional beam placement with spacing algorithms
  - Structural type management (beams vs. joists)

#### 2. **Vector Mathematics Implementation**

Key geometric operations demonstrated:

```csharp
// Compute perpendicular offset direction in 3D space
XYZ offsetDirection = faceNormal.CrossProduct(lineDirection).Normalize();

// Create offset geometry using vector addition
XYZ offsetStartPoint = line.GetEndPoint(0) + offsetDirection * offsetDistance;

// Tangent vector calculation for curve discretization
XYZ tangent = (pointsOnCurve[i + 1] - pointsOnCurve[i - 1]).Normalize();
```

#### 3. **3D Selection & Filtering** (`Filters/`)
Custom selection filters for 3D object interaction:

- **`BeamSelectionFilter`**: Structural framing element filtering
- **`WallSelectionFilter`**: Wall geometry filtering
- **`FramingObjectFilter`**: Generic 3D object classification

#### 4. **Configuration Management** (`Services/`)
Robust data structures for 3D modeling parameters:

```csharp
public class GridConfig
{
    public GridOrientation Orientation { get; set; }  // Spatial orientation
    public FamilySymbol FamilySymbol { get; set; }    // 3D geometry definition
    public double Spacing { get; set; }               // Grid spacing in feet
    public double ZOffset { get; set; }               // Vertical offset
    public StructuralType StructuralType { get; set; } // Element classification
}
```

---

## 🧮 Demonstrated 3D Programming Skills

### Vector Mathematics & Linear Algebra
✅ **Cross Product Operations**: Computing perpendicular vectors for offset geometry
✅ **Normalization**: Unit vector calculations for direction consistency
✅ **Dot Products**: Angle calculations and projection operations
✅ **Vector Addition/Subtraction**: 3D point transformations
✅ **Distance Calculations**: Euclidean distance in 3D space

### 3D Geometry Algorithms
✅ **Surface Analysis**: Finding optimal flat faces from complex 3D meshes
✅ **Edge Traversal**: Walking edge loops to extract boundary geometry
✅ **Curve Offsetting**: Parallel curve generation in 3D space
✅ **Curve Tessellation**: Discretization of complex curves into linear segments
✅ **Bounding Box Computation**: Spatial extent calculations
✅ **Containment Testing**: Point-in-polygon and region tests

### 3D Modeling & Rendering
✅ **Parametric 3D Modeling**: Family symbol instantiation with transforms
✅ **Level of Detail Management**: Performance optimization for large models
✅ **3D Coordinate Systems**: Local vs. global coordinate transformations
✅ **Element Visualization**: Real-time 3D preview generation

### CAD-Specific Programming
✅ **Transaction Management**: Atomic operations for model consistency
✅ **Element Lifecycle**: Creation, modification, deletion workflows
✅ **Category Management**: Object classification and filtering
✅ **Parameter Handling**: Reading/writing element properties

---

## 🚀 Getting Started

### Prerequisites
- **Visual Studio 2022** (or later)
- **.NET 8.0 SDK**
- **Autodesk Revit 2025**
- Windows 10/11 (x64)

### Building the Project

```bash
# Clone the repository
git clone https://github.com/yourusername/BoltFramePlugin.git
cd BoltFramePlugin

# Build the solution
dotnet build BoltFramePlugin.sln -c Release

# The build process will:
# 1. Terminate running Revit instances (prebuild.bat)
# 2. Compile the plugin DLL
# 3. Sign the assembly with digital certificate
# 4. Launch Revit 2025 with test project (post-build.ps1)
```

### Development Workflow

The plugin includes automated build steps for efficient development:

1. **Pre-Build**: Kills running Revit.exe instances
2. **Build**: Compiles C# code to .NET assembly
3. **Post-Build**:
   - Digitally signs the DLL
   - Launches Revit 2025 with test file (`Bed.rvt`)
   - Hot-reload ready for testing

### Installation

1. Build the project in **Release** mode
2. Copy `BoltFramePlugin.dll` to Revit's plugins folder:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2025\
   ```
3. Create or update the `.addin` manifest file
4. Launch Revit 2025

---

## 📐 Usage Examples

### Basic Floor Framing Generation

```csharp
// 1. User selects floor in 3D view
Floor floor = /* selected floor element */;

// 2. Configure framing parameters
var model = new FloorFrameGenerateModel
{
    TargetElement = floor,
    Level = floor.LevelId,
    BeamSymbol = selectedBeamType,
    ColSymbol = selectedColumnType,
    JoistSpacing = 2.0,  // feet
    BeamSpacing = 8.0    // feet
};

// 3. Execute framing strategy
var strategy = FramingStrategyFactory.GetFramingStrategy(revitService, model);
strategy.StartGenerate();
```

### Multi-Floor Batch Processing

```csharp
// Configure grid patterns
var gridConfigs = new List<GridConfig>
{
    new GridConfig
    {
        Orientation = GridOrientation.Horizontal,
        FamilySymbol = beamSymbol,
        Spacing = 2.0,
        ZOffset = 0.0,
        StructuralType = StructuralType.Beam
    },
    new GridConfig
    {
        Orientation = GridOrientation.Vertical,
        FamilySymbol = joistSymbol,
        Spacing = 1.5,
        ZOffset = -0.5,
        StructuralType = StructuralType.Joist
    }
};

// Apply to multiple floors
var multiFloorModel = new MultiFloorFrameGenerateModel
{
    TargetElements = selectedFloors,
    GridConfigs = gridConfigs
};

var strategy = new MultiFloorFramingStrategy(revitService, multiFloorModel);
strategy.StartGenerate();
```

---

## 🎓 Learning Outcomes & Skill Development

This project demonstrates professional proficiency in:

### Core 3D Programming Competencies
- **3D Mathematics**: Vector operations, transformations, geometric calculations
- **Computational Geometry**: Algorithms for curve offsetting, surface analysis, tessellation
- **Object-Oriented Design**: SOLID principles, design patterns, clean architecture
- **3D API Integration**: Deep knowledge of Autodesk Revit API architecture
- **Performance Optimization**: Efficient algorithms for large-scale 3D data processing

### Software Engineering Best Practices
- **Dependency Injection**: Loosely coupled, testable architecture
- **Logging & Diagnostics**: Comprehensive error tracking with Serilog
- **Configuration Management**: Flexible, user-customizable settings
- **Event-Driven Architecture**: Responsive to document lifecycle events
- **Transaction Safety**: ACID-compliant model modifications

### Domain Knowledge
- **Structural Engineering**: Understanding of framing systems and load paths
- **Construction Practices**: Real-world building assembly sequences
- **BIM Workflows**: Integration with industry-standard processes
- **CAD Standards**: Proper element categorization and documentation

---

## 📊 Project Statistics

- **Total Code Lines**: ~5,000+ lines of C#
- **Core Classes**: 50+ custom classes
- **Design Patterns**: 6 major patterns implemented
- **3D Algorithms**: 15+ geometric operations
- **UI Components**: 8 custom WPF views and controls

---

## 🔍 Code Quality & Architecture

### Dependency Injection Container

```csharp
public static class DIContainerService
{
    public static void RegisterServices()
    {
        container.RegisterSingleton<ILoggingService, LoggingService>();
        container.RegisterSingleton<IRevitServiceFactory, RevitServiceFactory>();
        container.RegisterSingleton<IThemeService, ThemeService>();
        container.RegisterSingleton<IFileService, FileService>();
        container.RegisterSingleton<IPluginConfigurationManager, PluginConfigurationManager>();
        container.RegisterSingleton<IProjectConfigurationManager, ProjectConfigurationManager>();
        container.RegisterInstance<IWindowManager>(new WindowManager());

        container.Verify();
    }
}
```

### Logging Infrastructure

All operations are logged to:
```
%APPDATA%\Revit\BoltFramePlugin\BoltFramePlugin-{date}.log
```

- Rolling daily logs (30-day retention)
- Structured logging with timestamps
- Exception tracking with stack traces

---

## 📚 Documentation

- **[CLAUDE.md](CLAUDE.md)**: Comprehensive development guide for AI-assisted coding
- **Code Comments**: XML documentation on all public APIs
- **Inline Comments**: Detailed explanations of complex geometric algorithms

---

## 🛠️ Future Enhancements

Planned features that would further demonstrate 3D programming expertise:

- [ ] **3D Visualization Dashboard**: Real-time cost estimation based on generated geometry
- [ ] **Material Optimization**: AI-driven beam sizing based on structural analysis
- [ ] **Collision Detection**: 3D spatial conflict checking for MEP coordination
- [ ] **Export to Excel**: Automated quantity takeoffs and material schedules
- [ ] **Custom 3D Rendering**: OpenGL/DirectX overlay for enhanced visualization
- [ ] **Parametric Design Tools**: User-defined formulas for custom framing patterns

---

## 👨‍💻 About the Developer

This project showcases my expertise in:

✅ **3+ years** of professional C# development
✅ **Deep understanding** of object-oriented programming and design patterns
✅ **Strong background** in vector mathematics, linear algebra, and 3D geometry
✅ **Proven experience** in CAD automation and construction software
✅ **Excellent problem-solving** skills with complex geometric algorithms
✅ **Production-ready code** with proper error handling and logging

### Alignment with Seljax 3D Programmer Position

This project directly demonstrates:

- ✅ Visual Studio + C# + .NET development
- ✅ Object-oriented programming and design patterns
- ✅ Vector mathematics and 3D geometry expertise
- ✅ Data structures and algorithms implementation
- ✅ 3D modeling and rendering system design
- ✅ CAD-like design/automation application experience
- ✅ Construction project domain knowledge


---

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

---

## 🙏 Acknowledgments

- Autodesk Revit API for comprehensive 3D modeling capabilities
- The Building SMART community for BIM standards
- Open-source contributors to SimpleInjector and Serilog

---

**Built with precision. Engineered for performance. Designed for professionals.**
