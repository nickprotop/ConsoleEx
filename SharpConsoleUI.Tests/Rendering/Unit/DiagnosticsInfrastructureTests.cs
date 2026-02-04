// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit;

/// <summary>
/// Tests for the rendering diagnostics infrastructure.
/// Validates that diagnostics capture works correctly across all three layers.
/// </summary>
public class DiagnosticsInfrastructureTests
{
	[Fact]
	public void Diagnostics_WhenEnabled_CapturesSnapshots()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 40, Height = 20 };
		var markup = new MarkupControl(new List<string> { "Test" });
		window.AddControl(markup);

		// Act
		window.RenderAndGetVisibleContent();

		// Assert
		var diagnostics = system.RenderingDiagnostics;
		Assert.NotNull(diagnostics);
		Assert.True(diagnostics.IsEnabled);
	}

	[Fact]
	public void Diagnostics_CapturesCharacterBufferSnapshot()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 50, Height = 25 };
		var markup = new MarkupControl(new List<string> { "[red]Hello[/]" });
		window.AddControl(markup);

		// Act
		window.RenderAndGetVisibleContent();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastBufferSnapshot;
		Assert.NotNull(snapshot);
		Assert.Equal(50, snapshot.Width);
		Assert.Equal(25, snapshot.Height);
	}

	[Fact]
	public void Diagnostics_CapturesAnsiSnapshot()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 40, Height = 15 };
		var markup = new MarkupControl(new List<string> { "Content" });
		window.AddControl(markup);

		// Act
		window.RenderAndGetVisibleContent();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(snapshot);
		Assert.NotEmpty(snapshot.Lines);
		Assert.True(snapshot.TotalAnsiEscapes >= 0);
	}

	[Fact]
	public void Diagnostics_WhenDisabled_DoesNotCapture()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		var window = new Window(system) { Width = 40, Height = 20 };
		var markup = new MarkupControl(new List<string> { "Test" });
		window.AddControl(markup);

		// Act
		window.RenderAndGetVisibleContent();

		// Assert
		Assert.Null(system.RenderingDiagnostics);
	}

	[Fact]
	public void Diagnostics_MultipleFrames_RetainsHistory()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 40, Height = 20 };
		var markup = new MarkupControl(new List<string> { "Frame 1" });
		window.AddControl(markup);

		// Act - Render frame 1
		window.RenderAndGetVisibleContent();
		var frame1Number = system.RenderingDiagnostics!.CurrentFrameNumber;

		// Render frame 2 (invalidate and render again)
		window.Invalidate(true);
		window.RenderAndGetVisibleContent();
		var frame2Number = system.RenderingDiagnostics.CurrentFrameNumber;

		// Assert
		// Frame numbers should be different
		Assert.True(frame2Number > frame1Number);
	}

	[Fact]
	public void Diagnostics_CanQueryByFrameNumber()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 40, Height = 20 };
		var markup = new MarkupControl(new List<string> { "Test" });
		window.AddControl(markup);

		// Act
		window.RenderAndGetVisibleContent();
		var frameNumber = system.RenderingDiagnostics!.CurrentFrameNumber;

		// Assert
		var snapshot = system.RenderingDiagnostics.GetSnapshot<Diagnostics.Snapshots.CharacterBufferSnapshot>(frameNumber);
		Assert.NotNull(snapshot);
		Assert.Equal(frameNumber, snapshot.FrameNumber);
	}

	[Fact]
	public void Diagnostics_CapturesMetrics()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 40, Height = 20 };
		var markup = new MarkupControl(new List<string> { "Test Metrics" });
		window.AddControl(markup);

		// Act
		window.RenderAndGetVisibleContent();

		// Assert
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);
		Assert.True(metrics.FrameNumber > 0);
	}

	[Fact]
	public void Diagnostics_QualityAnalysis_GeneratesReport()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 40, Height = 20 };
		var markup = new MarkupControl(new List<string> { "Quality Test" });
		window.AddControl(markup);

		// Act
		window.RenderAndGetVisibleContent();

		// Assert
		var qualityReport = system.RenderingDiagnostics?.LastQualityReport;
		Assert.NotNull(qualityReport);
		Assert.True(qualityReport.FrameNumber > 0);
		Assert.True(qualityReport.OptimizationScore >= 0 && qualityReport.OptimizationScore <= 1.0);
	}
}
