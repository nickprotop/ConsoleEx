// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// Reading what a headless app put on screen, as text.
/// </summary>
/// <remarks>
/// The driver could be driven but not read: <c>FullOutput</c> is raw ANSI, so
/// the only way to assert on a line was to strip escapes with a regex. These
/// cover the accessor that replaces that.
/// </remarks>
public class HeadlessTextReadingTests
{
	private static ConsoleWindowSystem Headless(HeadlessConsoleDriver driver) =>
		new(driver, ConsoleWindowSystemOptions.Create() with
		{
			PipedInput = new PipedInputOptions(Enabled: false)
		});

	[Fact]
	public void ToLines_reads_what_a_window_painted()
	{
		var driver = new HeadlessConsoleDriver(40, 10);
		var system = Headless(driver);

		new WindowBuilder(system)
			.WithTitle("T")
			.WithSize(30, 5)
			.AddControl(SharpConsoleUI.Builders.Controls.Markup().AddLine("Wanda Wombat").Build())
			.BuildAndShow();

		system.Render.UpdateDisplay();

		var lines = driver.CaptureComposed()!.ToLines();

		Assert.Contains(lines, l => l.Contains("Wanda Wombat"));
	}

	[Fact]
	public void ToLines_returns_one_string_per_row()
	{
		var driver = new HeadlessConsoleDriver(40, 10);
		var system = Headless(driver);
		system.Render.UpdateDisplay();

		var lines = driver.CaptureComposed()!.ToLines();

		Assert.Equal(10, lines.Count);
		Assert.All(lines, l => Assert.Equal(40, l.Length));
	}

	[Fact]
	public void ToLines_does_not_double_a_wide_character()
	{
		var driver = new HeadlessConsoleDriver(40, 10);
		var system = Headless(driver);

		new WindowBuilder(system)
			.WithTitle("T")
			.WithSize(30, 5)
			.AddControl(SharpConsoleUI.Builders.Controls.Markup().AddLine("日本").Build())
			.BuildAndShow();

		system.Render.UpdateDisplay();

		var text = string.Join("\n", driver.CaptureComposed()!.ToLines());

		// Two glyphs over four columns: the continuation cell must not
		// repeat the character.
		Assert.Contains("日本", text);
		Assert.DoesNotContain("日日", text);
	}
}
