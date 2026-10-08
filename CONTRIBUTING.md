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

**Please read [`docs/CODE_QUALITY.md`](docs/CODE_QUALITY.md) before opening a PR** — it is
the full set of standards your code will be reviewed against. The highlights:

- **No breaking changes.** SharpConsoleUI has real NuGet users — never remove/rename a
  public API or change an existing member's signature or default behavior. Add overloads
  instead. This is the most important rule.
- Follow existing code style and patterns (tabs for indentation, fluent builders, the
  file-header banner on new source files).
- Never use `Console.WriteLine()` or any console output in library code — it corrupts the
  UI rendering. Use the built-in `LogService` for debug output.
- No magic numbers — use named constants in `Configuration/ControlDefaults.cs`.
- Extract shared logic to helper classes rather than duplicating code.
- Keep files under the size limits, render Unicode-correctly, and marshal UI mutations to
  the UI thread — details and examples in [`docs/CODE_QUALITY.md`](docs/CODE_QUALITY.md).

**Want a worked example?** The **[Contributor Tutorials](docs/tutorials/contributing/README.md)** walk
you through building a composite control, a primitive control (`BadgeControl`), and a dialog
(`Dialogs.PickAsync`) end to end — from an empty file to an open PR.

## Before You Push: the CI gates

CI blocks a PR on five checks. Each one runs locally with a single command, so none of them
needs to be discovered from a red build:

```bash
# 1. Formatting. Tabs, and whatever else the analyzers normalise.
dotnet format SharpConsoleUI/SharpConsoleUI.csproj

# 2. The library must build warning-clean (CS/CA/IDE/IL, every target framework).
dotnet build SharpConsoleUI/SharpConsoleUI.csproj -c Release --no-incremental

# 3. The whole suite, which is where a regression usually shows up first.
dotnet test SharpConsoleUI.Tests/SharpConsoleUI.Tests.csproj

# 4. NativeAOT. Trim and AOT analyzer warnings are errors here.
dotnet publish SharpConsoleUI.Tests/aot.test/AotSmoke.csproj -c Release
```

`NU1902` on `SixLabors.ImageSharp` is expected and is not one of the gates. The advisory it
points at is in the TIFF decoder, which the library refuses to use — see `PixelBuffer`. NuGet
flags the package rather than the usage, so the notice stays until the dependency changes.

The fifth is the file-header banner: every `.cs` file under `SharpConsoleUI/` needs the
Author / Email / `License: MIT` block in its first eight lines. Copy it from any neighbouring
file when you add one.

A sixth check reports file sizes but does not block — see the limits in
[`docs/CODE_QUALITY.md`](docs/CODE_QUALITY.md).

## Tests

A change to behaviour wants a test, and the suite is large enough that adding one is usually
quick. Two things are worth knowing, both learned the hard way:

- **Assert that state survives a re-render.** A test that checks a value right after an action,
  and never renders again, passes against bugs that drop that state on the next layout pass.
- **Include one test that drives the real path.** Build the real container nesting, use the real
  input route (`InputStateService.EnqueueKey` + `system.Input.ProcessInput` for keys), and assert
  the observable end state. Isolated component tests have gone green while the live app was
  visibly broken.

For anything touching scrolling, the mouse or layout, also drive the DemoApp and look at it.

## Submitting Changes

1. Run the gates above
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
