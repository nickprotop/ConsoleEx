// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.WindowManagement;

/// <summary>
/// Tests for BorderStyle.Frameless: zero-inset content, per-side padding, and the inset accessors.
/// </summary>
public class FramelessWindowTests
{
	private const int W = 40;
	private const int H = 20;

	private static (ConsoleWindowSystem sys, Window win) MakeWindow()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new Window(sys) { Width = W, Height = H, Left = 5, Top = 3 };
		return (sys, win);
	}

	[Fact]
	public void Bordered_DefaultPadding_InsetsAreOne()
	{
		var (_, win) = MakeWindow();
		win.BorderStyle = BorderStyle.DoubleLine;
		Assert.Equal(W - 2, win.ContentWidth);
		Assert.Equal(H - 2, win.ContentHeight);
		Assert.Equal(new System.Drawing.Point(6, 4), win.ContentOrigin);
	}

	[Fact]
	public void Frameless_NoPadding_ContentFillsRect()
	{
		var (_, win) = MakeWindow();
		win.BorderStyle = BorderStyle.Frameless;
		Assert.Equal(W, win.ContentWidth);
		Assert.Equal(H, win.ContentHeight);
		Assert.Equal(new System.Drawing.Point(5, 3), win.ContentOrigin);
	}

	[Fact]
	public void Frameless_UniformPadding_InsetsByPadding()
	{
		var (_, win) = MakeWindow();
		win.BorderStyle = BorderStyle.Frameless;
		win.Padding = new Padding(2);
		Assert.Equal(W - 4, win.ContentWidth);
		Assert.Equal(H - 4, win.ContentHeight);
		Assert.Equal(new System.Drawing.Point(7, 5), win.ContentOrigin);
	}

	[Fact]
	public void AsymmetricPadding_PerSideInsetsAreCorrect()
	{
		var (_, win) = MakeWindow();
		win.BorderStyle = BorderStyle.Frameless;
		win.Padding = new Padding(3, 1, 0, 2);
		Assert.Equal(W - 3 - 0, win.ContentWidth);
		Assert.Equal(H - 1 - 2, win.ContentHeight);
		Assert.Equal(new System.Drawing.Point(5 + 3, 3 + 1), win.ContentOrigin);
		Assert.Equal(3, win.InsetLeft);
		Assert.Equal(1, win.InsetTop);
		Assert.Equal(0, win.InsetRight);
		Assert.Equal(2, win.InsetBottom);
	}

	[Fact]
	public void Bordered_WithPadding_AddsToFrame()
	{
		var (_, win) = MakeWindow();
		win.BorderStyle = BorderStyle.Single;
		win.Padding = new Padding(2, 1, 2, 1);
		Assert.Equal(W - (1 + 2) * 2, win.ContentWidth);
		Assert.Equal(H - (1 + 1) * 2, win.ContentHeight);
	}

	[Fact]
	public void DegeneratePadding_ClampsToZero()
	{
		var (_, win) = MakeWindow();
		win.BorderStyle = BorderStyle.Frameless;
		win.Padding = new Padding(100);
		Assert.Equal(0, win.ContentWidth);
		Assert.Equal(0, win.ContentHeight);
	}

	[Fact]
	public void Builder_Frameless_SetsBorderStyleAndFillsContent()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new WindowBuilder(sys).WithSize(30, 12).Frameless().Build();
		Assert.Equal(BorderStyle.Frameless, win.BorderStyle);
		Assert.Equal(30, win.ContentWidth);
		Assert.Equal(12, win.ContentHeight);
	}

	[Fact]
	public void Builder_WithPadding_Uniform()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new WindowBuilder(sys).WithSize(30, 12).Frameless().WithPadding(2).Build();
		Assert.Equal(new Padding(2), win.Padding);
		Assert.Equal(30 - 4, win.ContentWidth);
	}

	[Fact]
	public void Builder_WithPadding_PerSide()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new WindowBuilder(sys).WithSize(30, 12).Frameless()
			.WithPadding(new Padding(3, 1, 0, 2)).Build();
		Assert.Equal(new Padding(3, 1, 0, 2), win.Padding);
	}

	[Fact]
	public void Builder_WithPadding_HorizontalVertical()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new WindowBuilder(sys).WithSize(30, 12).Frameless().WithPadding(4, 1).Build();
		Assert.Equal(new Padding(4, 1), win.Padding);
	}

	[Fact]
	public void Frameless_ScrollbarShown_ReservesLastColumn()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new Window(sys) { Width = 20, Height = 6, BorderStyle = BorderStyle.Frameless, IsScrollable = true };
		Assert.False(win.FramelessScrollbarReserved(totalLines: 6));   // fits (== ContentHeight)
		Assert.True(win.FramelessScrollbarReserved(totalLines: 7));    // overflows
		Assert.Equal(win.ContentWidth, win.FramelessLayoutWidth(totalLines: 6));      // full width
		Assert.Equal(win.ContentWidth - 1, win.FramelessLayoutWidth(totalLines: 7)); // reserved
	}

	[Fact]
	public void NonScrollable_Frameless_NeverReservesColumn()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new Window(sys) { Width = 20, Height = 6, BorderStyle = BorderStyle.Frameless, IsScrollable = false };
		Assert.False(win.FramelessScrollbarReserved(totalLines: 999));
		Assert.Equal(win.ContentWidth, win.FramelessLayoutWidth(totalLines: 999));
	}

	[Fact]
	public void Bordered_NeverReservesFramelessColumn()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(80, 30));
		var win = new Window(sys) { Width = 20, Height = 6, BorderStyle = BorderStyle.DoubleLine, IsScrollable = true };
		Assert.False(win.FramelessScrollbarReserved(totalLines: 999)); // only Frameless reserves
		Assert.Equal(win.ContentWidth, win.FramelessLayoutWidth(totalLines: 999));
	}

	/// <summary>
	/// Regression: a frameless scrollable window with overflowing content must lay its content out
	/// ONE column narrower than the full content width, so the rendered content buffer cannot reach
	/// (and overwrite) the reserved scrollbar column. The blit clamps the right edge to the buffer
	/// width; this asserts the buffer really is narrowed. (A tmux visual gate caught the original bug
	/// where the content blit painted over the scrollbar column.)
	/// </summary>
	[Fact]
	public void Frameless_OverflowingContent_BufferIsNarrowedForScrollbar()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(100, 30));
		var lines = new System.Collections.Generic.List<string>();
		for (int i = 0; i < 80; i++) lines.Add($"line {i} of overflowing frameless content");
		var win = new WindowBuilder(sys).Frameless().Maximized()
			.AddControl(new SharpConsoleUI.Controls.MarkupControl(lines)).BuildAndShow();

		var buffer = win.EnsureContentReady();

		Assert.True(win.FramelessScrollbarReserved(win.TotalLines));
		Assert.Equal(win.ContentWidth - 1, win.FramelessLayoutWidth(win.TotalLines));
		Assert.NotNull(buffer);
		// The content buffer is the reserved (narrowed) width, so content stops before the scrollbar column.
		Assert.Equal(win.ContentWidth - 1, buffer!.Width);
	}

	[Fact]
	public void Frameless_NonOverflowingContent_BufferUsesFullWidth()
	{
		var sys = new ConsoleWindowSystem(new MockConsoleDriver(100, 30));
		var win = new WindowBuilder(sys).Frameless().Maximized()
			.AddControl(new SharpConsoleUI.Controls.MarkupControl(new System.Collections.Generic.List<string> { "short" })).BuildAndShow();

		var buffer = win.EnsureContentReady();

		Assert.False(win.FramelessScrollbarReserved(win.TotalLines));
		Assert.NotNull(buffer);
		Assert.Equal(win.ContentWidth, buffer!.Width);
	}
}
