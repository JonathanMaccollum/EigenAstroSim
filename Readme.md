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

- **EigenAstroSim.Domain**: Core domain model and business logic
  - **Types**: Core domain types
  - **StarField**: Star field generation and management
  - **StarFieldGenerator**: Algorithms for creating realistic star distributions
  - **ImageGeneration**: Synthetic image creation with realistic noise and effects
  - **Mount**: Mount simulation with tracking and guiding (in progress)
  
- **EigenAstroSim.Tests**: Comprehensive test suite
  - **StarFieldTests**: Unit tests for star field functionality
  - **PropertyBasedTests**: Property-based tests for star field
  - **ImageGenerationTests**: Unit tests for image generation
  - **ImageGenerationPropertyTests**: Property-based tests for image generation

## Current Status

The project is under active development. Current implementation includes:

- ✅ Core domain types (Star, StarFieldState, MountState, CameraState, etc.)
- ✅ Star field generation with realistic distributions
- ✅ Comprehensive test suite for star field functionality
- ✅ Image generation with PSF rendering, atmospheric effects, and sensor simulation
- 🔄 Mount simulation (in progress)
- ⬜ Camera driver simulation
- ⬜ ASCOM drivers
- ⬜ UI implementation

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
- Visual Studio 2022 or JetBrains Rider (recommended for F# development)

### Building the Project

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
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

### Mount Simulation (In Progress)

The mount simulation will model:
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