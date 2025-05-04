# EigenAstroSim

EigenAstroSim is a desktop application that simulates astronomical equipment for astrophotography software development and testing. It provides ASCOM-compliant virtual devices (mount, camera, and rotator) that can be used by other astrophotography software for development and troubleshooting.

## Project Overview

The simulator creates a virtual night sky environment with realistic star fields, atmospheric conditions, and telescope mount behavior. It allows developers to test astrophotography software without actual hardware or clear night skies.

### Key Features

- ASCOM-compliant virtual mount driver with realistic tracking, slewing, and guiding
- ASCOM-compliant virtual guide camera with realistic star field imagery
- ASCOM-compliant virtual rotator with proper field rotation effects
- Realistic synthetic star field generation
- Simulated atmospheric seeing conditions, clouds, and transparency
- Accurate tracking issues, periodic errors, and mount behavior
- Realistic star rendering with proper point spread functions
- Sensor noise modeling
- Satellite trails and other artifacts

## Technology Stack

- **Language**: F# using Functional Programming principles
- **UI**: WPF with MVVM pattern
- **Framework**: .NET 8
- **Reactive Programming**: Rx.NET (Reactive Extensions for .NET)
- **Astronomy Equipment Interface**: ASCOM Platform

## Project Structure

The project is organized into the following main components:

### **Docs**
Documentation organized by milestone phases:
- **Milestone 0.0**: General best practices and guidelines established before development
- **Milestone 0.1**: Initial architecture design and conceptual brainstorming
- **Milestone 0.2**: Current implementation phase documentation and test plans
- **Milestone 0.3**: Advanced image simulation design for future enhancements

### **EigenAstroSim.Domain**
Core domain model and business logic:
- Domain types and data structures
- Star field generation algorithms
- Image generation with physical modeling
- Mount simulation and tracking algorithms
- Logging infrastructure

### **EigenAstroSim.Tests**
Comprehensive test suite using both unit and property-based testing approaches:
- Star field generation tests
- Image generation and processing tests
- Mount simulation and tracking tests
- Property-based tests for validating domain behaviors

### **EigenAstroSim.UI**
User interface logic and simulation coordination:
- Simulation engine (core coordination component)
- Timer services for simulation updates
- WPF value converters
- View models implementing MVVM pattern

### **EigenAstroSim.UI.Views**
WPF application and user interface components:
- Main application window and framework
- Equipment control views (mount, camera, rotator)
- Environmental simulation views (atmosphere, seeing)
- Star field visualization

## Simulation Engine

At the heart of EigenAstroSim is a powerful simulation engine that serves as the central coordinator for all virtual astronomical equipment. This engine provides a realistic, physics-based simulation of astrophotography conditions and equipment behavior.

### Core Concepts

The simulation engine:

- **Models real-world astronomy equipment** (mounts, cameras, rotators) with high fidelity
- **Simulates environmental conditions** such as atmospheric seeing, clouds, and transparency
- **Generates realistic imagery** including star fields, sensor noise, and optical effects
- **Coordinates time-dependent behaviors** across all components

### Component-Based Architecture

The engine uses a component-based architecture that mirrors the physical separation of astronomical equipment:

- **Mount component**: Handles telescope pointing, tracking, guiding, and modeling mechanical errors
- **Camera component**: Manages exposure timing, image generation, and sensor characteristics
- **Rotator component**: Controls image rotation and field orientation
- **Atmosphere component**: Simulates environmental conditions affecting image quality

This modular design allows each component to evolve independently while maintaining cohesive overall behavior.

### Message-Driven Communication

The simulation engine uses a message-based communication system:

1. **Equipment commands** are sent as messages to the appropriate components
2. **Components process commands** and update their internal state accordingly
3. **State changes generate events** that can be observed by UI and other components
4. **Image generation** occurs based on the combined state of all components

This approach enables asynchronous operation and clean separation between components, mimicking the way real astronomical equipment operates.

### Observable State Changes

All state changes in the simulation are exposed as observable streams, allowing:

- **Real-time UI updates** as equipment state changes
- **Reactive behavior chains** where one component responds to another's state
- **Temporal simulation** with precise timing control

### Extensibility

The simulation engine is designed for extensibility, allowing:

- **Addition of new equipment types** (like filter wheels, focusers, etc.)
- **Enhanced simulation models** with greater physical accuracy
- **Custom equipment behaviors** for testing edge cases
- **Integration with external systems** via the ASCOM interfaces

This extensible design ensures that EigenAstroSim can grow to accommodate new types of astronomical equipment and more sophisticated simulation needs.

## Current Status

The project is under active development. Current implementation includes:

- ✅ Core domain types (Star, StarFieldState, MountState, CameraState, etc.)
- ✅ Star field generation with realistic distributions
- ✅ Comprehensive test suite for star field functionality
- ✅ Image generation with PSF rendering, atmospheric effects, and sensor simulation
- ✅ Mount simulation
- ✅ WPF UI with MVVM pattern
- ✅ Application Logging with Serilog
- 🔄 Camera driver simulation
- 🔄 UI functionality completion
- ⬜ ASCOM drivers

## Development Approach

The project follows:

- **Functional Programming**: Using immutable data structures and function composition
- **Test-Driven Development**: With comprehensive unit and property-based tests
- **Reactive Programming**: Using Rx.NET for event-based and asynchronous operations
- **Message-Based Architecture**: Using message passing for state updates
- **MVVM Pattern**: For separation of concerns in the UI

## Getting Started

### Prerequisites

- .NET 8 SDK
- ASCOM Platform (latest version)
- Visual Studio Code

### Building the Project

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Running the Application

```bash
dotnet run --project .\EigenAstroSim.UI.Views\EigenAstroSim.UI.Views.csproj
```

## Component Details

### Star Field Generation

The star field generator creates realistic star distributions with:
- Magnitude distribution following astronomical power laws
- Star colors correlated with magnitude (color-magnitude relationship)
- Density variation based on galactic latitude
- Persistence of stars when revisiting areas

### Image Generation

The image generator produces realistic synthetic images with:
- Point spread functions based on seeing conditions
- Sensor noise (read noise, dark current, shot noise)
- Cloud coverage effects on star visibility
- Satellite trails
- Proper binning
- Exposure time scaling

### Mount Simulation

The mount simulation models:
- Equatorial mount mechanics
- Tracking and slewing
- Periodic error
- Polar alignment error
- Response to guide commands
- Cable snags and other common issues

## Usage

When completed, the application will provide:

1. A graphical interface to control and monitor the virtual equipment
2. ASCOM-compliant drivers that can be used by other applications
3. Configuration options for simulating various conditions and issues

## Future Plans

- Integration with real star catalogs (e.g., ASTAP)
- Main imaging camera simulation
- Additional virtual devices (focuser, filter wheel)
- Weather simulation
- More complex mount modeling

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

## License

[MIT License](LICENSE)