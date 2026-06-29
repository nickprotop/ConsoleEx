// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using Xunit;

namespace SharpConsoleUI.Tests.Infrastructure;

/// <summary>
/// Test harness for asserting the GEOMETRY of rendered chrome — where border and scrollbar characters
/// actually land on the composited screen — rather than control state (offsets, modes). This is the gap
/// that let issues #60/#61 ship: every existing test asserted scroll state, none asserted "is the bottom
/// border on its row" or "is the scrollbar in a single column". The harness renders the FULL window system
/// to a headless driver (so window compositing + the desktop/maximize clip are exercised, not just a
/// control's own paint) and reads the composited cells back via the rendering-diagnostics snapshot.
/// </summary>
public static class ChromeGeometry
{
	/// <summary>A rectangle on the composited screen, in screen (not desktop-relative) coordinates.</summary>
	public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
	{
		public int Right => X + Width - 1;   // inclusive last column
		public int Bottom => Y + Height - 1; // inclusive last row
	}

	/// <summary>
	/// Builds a diagnostics-enabled test system at the given screen size. The (width,height) builder
	/// overload already enables diagnostics and disables the top/bottom status panels — matching the
	/// issue repros, which set ShowTopPanel/ShowBottomPanel = false.
	/// </summary>
	public static ConsoleWindowSystem CreateSystem(int screenWidth, int screenHeight)
		=> TestWindowSystemBuilder.CreateTestSystem(screenWidth, screenHeight);

	/// <summary>
	/// Renders the system once and returns the composited-screen snapshot. The snapshot is the source of
	/// truth for geometry assertions — it is what the terminal would actually display.
	/// </summary>
	public static CharacterBufferSnapshot Render(ConsoleWindowSystem system)
	{
		system.Render.UpdateDisplay();
		var snapshot = system.RenderingDiagnostics?.LastBufferSnapshot;
		Assert.NotNull(snapshot);
		return snapshot!;
	}

	/// <summary>Reads the character at a composited-screen cell, or '\0' if out of bounds.</summary>
	public static char CharAt(CharacterBufferSnapshot snap, int x, int y)
	{
		if (x < 0 || y < 0 || x >= snap.Width || y >= snap.Height) return '\0';
		var s = snap.GetCell(x, y).Character.ToString();
		return s.Length > 0 ? s[0] : '\0';
	}

	/// <summary>Renders one composited-screen row as a string (for diagnostic messages).</summary>
	public static string Row(CharacterBufferSnapshot snap, int y)
	{
		if (y < 0 || y >= snap.Height) return string.Empty;
		var sb = new StringBuilder(snap.Width);
		for (int x = 0; x < snap.Width; x++) sb.Append(CharAt(snap, x, y));
		return sb.ToString();
	}

	/// <summary>
	/// Asserts that a rounded box border is COMPLETE on the composited screen: all four corners
	/// (╭╮╰╯) at the rect's corners, and that the bottom edge row carries the horizontal rule (─).
	/// This is the predicate that catches #60 — a maximized frameless panel whose bottom border row
	/// was clipped off the bottom of the screen.
	/// </summary>
	public static void AssertRoundedBoxComplete(CharacterBufferSnapshot snap, ScreenRect rect)
	{
		char tl = CharAt(snap, rect.X, rect.Y);
		char tr = CharAt(snap, rect.Right, rect.Y);
		char bl = CharAt(snap, rect.X, rect.Bottom);
		char br = CharAt(snap, rect.Right, rect.Bottom);

		Assert.True(tl == '╭', $"top-left corner ╭ missing at ({rect.X},{rect.Y}); got '{tl}'. Row: {Row(snap, rect.Y)}");
		Assert.True(tr == '╮', $"top-right corner ╮ missing at ({rect.Right},{rect.Y}); got '{tr}'. Row: {Row(snap, rect.Y)}");
		Assert.True(bl == '╰', $"bottom-left corner ╰ missing at ({rect.X},{rect.Bottom}); got '{bl}'. Row: {Row(snap, rect.Bottom)}");
		Assert.True(br == '╯', $"bottom-right corner ╯ missing at ({rect.Right},{rect.Bottom}); got '{br}'. Row: {Row(snap, rect.Bottom)}");

		// The bottom edge (between the corners) must be the horizontal rule.
		char mid = CharAt(snap, (rect.X + rect.Right) / 2, rect.Bottom);
		Assert.True(mid == '─', $"bottom border rule ─ missing mid-edge at row {rect.Bottom}; got '{mid}'. Row: {Row(snap, rect.Bottom)}");
	}
}
