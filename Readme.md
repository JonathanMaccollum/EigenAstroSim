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
  - Star field generation and management
  - Mount simulation
  - Camera simulation
  - Image generation
  
- **EigenAstroSim.Tests**: Comprehensive test suite
  - Property-based tests
  - Unit tests
  - Integration tests

## Current Status

The project is under active development. Current implementation includes:

- ✅ Core domain types (Star, StarFieldState, etc.)
- ✅ Star field generation with realistic distributions
- ✅ Comprehensive test suite for star field functionality
- 🔄 Image generation (in progress)
- ⬜ Mount simulation
- ⬜ Camera simulation
- ⬜ ASCOM drivers
- ⬜ UI implementation

## Development Approach

The project follows:

- Functional programming principles with immutable data structures
- Test-driven development with comprehensive unit and property-based tests
- Reactive programming patterns using Rx.NET
- MVVM architecture for UI components

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
