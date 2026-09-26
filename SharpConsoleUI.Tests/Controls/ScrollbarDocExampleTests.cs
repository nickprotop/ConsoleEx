// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Pins the scrollbar and wheel-step code samples in the docs against API drift — a doc example
/// that no longer compiles is worse than no example. Covers
/// <c>docs/controls/ScrollbarControl.md</c>, <c>docs/CONFIGURATION.md</c> and
/// <c>docs/BUILDERS.md</c>. If one of these fails, fix the doc as well as the test.
/// </summary>
[Collection("WheelStepDefault")]
public class ScrollbarDocExampleTests
{
	[Fact]
	public void DocExample_ScrollbarControlMd_CompositeDoesNotEcho()
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(80, 25),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 12, BorderStyle = BorderStyle.Frameless };

		ScrollablePanelControl MakePane() =>
			SharpConsoleUI.Builders.Controls.ScrollablePanel()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.AddControl(SharpConsoleUI.Builders.Controls.Markup()
					.AddLines(Enumerable.Range(1, 60).Select(i => $"line {i}").ToArray()).Build())
				.Build();

		var leftPane = MakePane();
		var rightPane = MakePane();

		var bar = SharpConsoleUI.Builders.Controls.Scrollbar()
			.WithMaximum(60)
			.WithViewportLength(10)
			.Build();

		// --- exactly as documented ---
		bar.UserValueChanged += (_, value) =>
		{
			leftPane.ScrollVerticalBy(value - leftPane.VerticalScrollOffset);
			rightPane.ScrollVerticalBy(value - rightPane.VerticalScrollOffset);
		};

		int writeBacks = 0;
		leftPane.Scrolled += (_, e) => { writeBacks++; bar.Value = e.VerticalOffset; };
		// --- end documented snippet ---

		window.AddControl(leftPane);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// A code-set must not re-enter UserValueChanged (that is the whole design).
		bar.Value = 5;
		Assert.True(writeBacks < 10, $"write-back did not settle: {writeBacks} iterations");
	}

	/// <summary>
	/// docs/CONFIGURATION.md documents that DefaultWindowScrollWheelLines FOLLOWS
	/// DefaultScrollWheelLines until it is set explicitly, at which point it pins. Pins the claim.
	/// </summary>
	[Fact]
	public void DocExample_WindowDefault_FollowsThenPins()
	{
		int originalControl = ControlDefaults.DefaultScrollWheelLines;
		try
		{
			ControlDefaults.ResetDefaultWindowScrollWheelLinesForTests();

			ControlDefaults.DefaultScrollWheelLines = 3;
			Assert.Equal(3, ControlDefaults.DefaultWindowScrollWheelLines);

			ControlDefaults.DefaultWindowScrollWheelLines = 5;
			Assert.Equal(5, ControlDefaults.DefaultWindowScrollWheelLines);
			Assert.Equal(3, ControlDefaults.DefaultScrollWheelLines);
		}
		finally
		{
			ControlDefaults.DefaultScrollWheelLines = originalControl;
			ControlDefaults.ResetDefaultWindowScrollWheelLinesForTests();
		}
	}

	/// <summary>
	/// The BUILDERS.md ScrollbarBuilder example: OnUserValueChanged driving two panes.
	/// </summary>
	[Fact]
	public void DocExample_BuildersMd_ScrollbarBuilder_Compiles()
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(80, 25),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 12, BorderStyle = BorderStyle.Frameless };

		ScrollablePanelControl Pane() =>
			SharpConsoleUI.Builders.Controls.ScrollablePanel()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.AddControl(SharpConsoleUI.Builders.Controls.Markup()
					.AddLines(Enumerable.Range(1, 200).Select(i => $"line {i}").ToArray()).Build())
				.Build();

		var leftPane = Pane();
		var rightPane = Pane();

		var bar = SharpConsoleUI.Builders.Controls.Scrollbar()
			.WithMaximum(200)
			.WithViewportLength(20)
			.OnUserValueChanged((s, value) =>
			{
				leftPane.ScrollVerticalBy(value - leftPane.VerticalScrollOffset);
				rightPane.ScrollVerticalBy(value - rightPane.VerticalScrollOffset);
			})
			.Build();

		window.AddControl(leftPane);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		Assert.Equal(200, bar.Maximum);
		Assert.Equal(20, bar.ViewportLength);
	}

	[Fact]
	public void DocExample_AppWideKnob_Compiles()
	{
		int original = ControlDefaults.DefaultScrollWheelLines;
		try
		{
			ControlDefaults.DefaultScrollWheelLines = 3;
			Assert.Equal(3, ControlDefaults.DefaultScrollWheelLines);

			var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
				.WithMouseWheelScrollSpeed(3)
				.Build();
			Assert.Equal(3, panel.MouseWheelScrollSpeed);
		}
		finally
		{
			ControlDefaults.DefaultScrollWheelLines = original;
		}
	}
}
