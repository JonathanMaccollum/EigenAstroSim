# Contributing to EigenAstroSim

We welcome contributions to EigenAstroSim! This document provides guidelines for contributing to the project.

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YourUsername/EigenAstroSim.git`
3. Add the upstream repository: `git remote add upstream https://github.com/OriginalOwner/EigenAstroSim.git`
4. Install the prerequisites mentioned in the README

## Development Workflow

1. Create a feature branch from `main`: `git checkout -b feature/your-feature`
2. Make your changes, following our coding standards
3. Write tests for your changes
4. Commit using conventional commit messages
5. Create a pull request targeting `main`
6. Upon approval and passing CI, your PR will be squash merged

## Coding Standards

### F# Style Guide

Follow the guidelines in our documentation:
- **F# Best Practices and Common Pitfalls with .NET 8.md**
- **EigenAstroSim.00 - Fsharp Best Practices.md**

Key points:
- Use immutable data structures by default
- Follow functional-first principles
- Write comprehensive tests using FsUnit and property-based testing
- Prefer composition over inheritance
- Keep functions small and focused

### Commit Message Format

We use conventional commits for automated versioning and changelog generation:

```
<type>(<scope>): <description>

<body>

<footer>
```

Types:
- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation only changes
- `style`: Code style changes (formatting, missing semi colons, etc)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `build`: Changes to the build system
- `ci`: CI/CD changes

Example:
```
feat(mount): add periodic error simulation

Implements realistic periodic error patterns in mount tracking simulation.
- Adds sinusoidal error pattern
- Configurable amplitude and period
- Updates mount simulation tests

Closes #42
```

## Versioning

We use semantic versioning. Our simplified workflow:

- **main**: Stable releases
- **feature/***: Development versions (pre-release)
- **hotfix/***: Emergency fixes

Versioning is automated based on conventional commits:
- `feat`: Minor version bump
- `fix`: Patch version bump
- `BREAKING CHANGE`: Major version bump

## Pull Request Process

1. Update the README.md with details of changes to the interface, if applicable
2. Update documentation and tests
3. Ensure all tests pass (CI will run automatically)
4. The PR will be squash merged after approval from at least one maintainer

## Testing

All pull requests require:
- Unit tests for new functionality
- Property-based tests for algorithms
- Integration tests where appropriate
- All tests must pass in CI

## Release Process

Releases are created manually:
1. Create a tag from `main`: `git tag v0.1.0`
2. Push the tag: `git push origin v0.1.0`
3. GitHub Actions will automatically:
   - Build and test
   - Generate release notes
   - Create NuGet packages
   - Create a GitHub release

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

## Contact

For questions, please open an issue or contact the maintainers.

Thank you for contributing to EigenAstroSim!