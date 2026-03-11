# Contributing to SharpConsoleUI

Thank you for your interest in contributing to SharpConsoleUI!

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/your-username/ConsoleEx.git`
3. Create a branch: `git checkout -b feature/your-feature`
4. Build: `dotnet build`
5. Test your changes by running the examples: `dotnet run --project Examples/DemoApp`

## Development Setup

- **.NET 8.0 SDK** or later is required
- Build the solution: `dotnet build ConsoleEx.sln`
- Run the demo app to verify: `dotnet run --project Examples/DemoApp`

## Code Guidelines

- Follow existing code style and patterns (tabs for indentation, fluent builders, etc.)
- Never use `Console.WriteLine()` or any console output in library code — it corrupts the UI rendering
- Use the built-in `LogService` for debug output
- Keep files under size limits (see CLAUDE.md for details)
- No magic numbers — use named constants in `Configuration/ControlDefaults.cs`
- Extract shared logic to helper classes rather than duplicating code

## Submitting Changes

1. Ensure the solution builds without errors: `dotnet build`
2. Test your changes with the DemoApp and relevant examples
3. Commit with a clear message describing what and why
4. Push to your fork and open a Pull Request
5. Describe your changes and link any related issues

## Pull Request Guidelines

- Keep PRs focused — one feature or fix per PR
- Include screenshots for UI changes
- Update documentation if adding new public APIs
- Add an example if introducing a new control or major feature

## Reporting Issues

- Use GitHub Issues to report bugs or request features
- Include steps to reproduce for bugs
- Include your OS, .NET version, and terminal emulator

## Questions?

Open a GitHub Discussion or reach out to the maintainer at nikolaos.protopapas@gmail.com.
