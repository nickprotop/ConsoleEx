// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Render-level tests verifying that a <see cref="MarkupControl"/>'s explicit
/// <see cref="MarkupControl.BackgroundColor"/> is honoured by the main fill
/// (i.e. the cells carrying the text glyph) and that it also acts as the
/// background for the right-side fill when the content is narrower than the
/// available width.
/// </summary>
public class MarkupControlBackgroundColorTests
{
	private const int Width = 20;
	private const int Height = 3;

	private static readonly Color TestBg = new Color(100, 150, 200);
	private static readonly Color ContainerBg = new Color(255, 0, 0);

	private static CharacterBuffer PaintWithContainer(MarkupControl control)
	{
		var buffer = new CharacterBuffer(Width, Height);
		var bounds = new LayoutRect(0, 0, Width, Height);

		// Create a mock container that returns our known background.
		var mockContainer = new MockContainer(ContainerBg, new Color(255, 255, 255));
		control.Container = mockContainer;

		control.PaintDOM(buffer, bounds, bounds, new Color(255, 255, 255), new Color(0, 0, 0));
		return buffer;
	}

	private static CharacterBuffer PaintDirect(MarkupControl control)
	{
		var buffer = new CharacterBuffer(Width, Height);
		var bounds = new LayoutRect(0, 0, Width, Height);
		control.PaintDOM(buffer, bounds, bounds, new Color(255, 255, 255), new Color(0, 0, 0));
		return buffer;
	}

	/// <summary>
	/// Returns the background colour of the first cell on row 0 carrying the given character.
	/// </summary>
	private static Color? BackgroundOf(CharacterBuffer buffer, char target)
	{
		for (int x = 0; x < Width; x++)
		{
			var cell = buffer.GetCell(x, 0);
			if (cell.Character.Value == target)
				return cell.Background;
		}
		return null;
	}

	[Fact]
	public void ExplicitBackgroundColor_HonouredByMainFill_NoContainer()
	{
		// Arrange: build a MarkupControl with a known BackgroundColor and no container.
		var ctrl = new MarkupControl(new List<string> { "Hello" })
		{
			BackgroundColor = TestBg
		};

		// Act: paint directly to a buffer (no container is set).
		var buffer = PaintDirect(ctrl);

		// Assert: a cell carrying the text glyph should carry the control's BackgroundColor,
		// NOT the default background (black).
		var bg = BackgroundOf(buffer, 'e');
		Assert.NotNull(bg);
		Assert.Equal(TestBg, bg!.Value);
	}

	[Fact]
	public void ExplicitBackgroundColor_HonouredByMainFill_WithContainer()
	{
		// Arrange: a MarkupControl with its own BackgroundColor and a container.
		// The control's own BackgroundColor should take priority over the container's.
		var ctrl = new MarkupControl(new List<string> { "Hello" })
		{
			BackgroundColor = TestBg
		};
		var buffer = PaintWithContainer(ctrl);

		// Assert: the text cell should carry the CONTROL's BackgroundColor (not the container's red).
		var bg = BackgroundOf(buffer, 'e');
		Assert.NotNull(bg);
		Assert.Equal(TestBg, bg!.Value);
		Assert.NotEqual(ContainerBg, bg.Value);
	}

	[Fact]
	public void NoBackgroundColor_FallsBackToContainer()
	{
		// Arrange: a MarkupControl with BackgroundColor NOT set, placed in a container.
		// The control should inherit the container's BackgroundColor.
		var ctrl = new MarkupControl(new List<string> { "World" });
		var buffer = PaintWithContainer(ctrl);

		// Assert: the text cell should carry the container's background color (red).
		var bg = BackgroundOf(buffer, 'W');
		Assert.NotNull(bg);
		Assert.Equal(ContainerBg, bg!.Value);
	}

	[Fact]
	public void NoBackgroundColor_FallsBackToDefaultBg_WhenNoContainer()
	{
		// Arrange: a MarkupControl with BackgroundColor NOT set and no container.
		var ctrl = new MarkupControl(new List<string> { "World" });
		var buffer = PaintDirect(ctrl);

		// Assert: should fall back to the defaultBg (black).
		var bg = BackgroundOf(buffer, 'o');
		Assert.NotNull(bg);
		Assert.Equal(new Color(0, 0, 0), bg!.Value);
	}

	private sealed class MockContainer : IContainer
	{
		private readonly Color _bg;
		private readonly Color _fg;
		public MockContainer(Color bg, Color fg) { _bg = bg; _fg = fg; }
		public Color BackgroundColor { get => _bg; set { } }
		public Color ForegroundColor { get => _fg; set { } }
		public ConsoleWindowSystem? GetConsoleWindowSystem => null;
		public void Invalidate(Invalidation work, IWindowControl? callerControl = null) { }
		public void Invalidate(bool redrawAll, IWindowControl? callerControl = null) => Invalidate(redrawAll ? Invalidation.Relayout : Invalidation.Repaint, callerControl);
		public int? GetVisibleHeightForControl(IWindowControl control) => null;
	}

	/// <summary>
	/// A line's TRAILING SPACE must carry the same background as its text.
	///
	/// <para>The right-hand fill fell back to Transparent when the control had no explicit background
	/// of its own, so a MarkupControl inside a coloured container painted its text on the container's
	/// surface and the rest of the row on whatever sat behind. Measured live in a chat transcript: a
	/// message body carried the panel colour up to its last character and reverted for the remainder,
	/// while the header — filled by CollapsiblePanel itself — ran the full width. One block, two
	/// colours, split at whatever column the text happened to end on.</para>
	/// </summary>
	[Fact]
	public void TrailingSpaceCarriesTheContainerBackground()
	{
		var ctrl = new MarkupControl(new List<string> { "Hi" });
		var buffer = PaintWithContainer(ctrl);

		// A column PAST the text, still inside the control's bounds.
		var tail = buffer.GetCell(Width - 1, 0).Background;

		Assert.Equal(ContainerBg, tail);
	}

	[Fact]
	public void AnExplicitBackgroundStillWinsForTheTrailingSpace()
	{
		// The control's own colour keeps precedence over the container's — the fallback only applies
		// when the control has none.
		var ctrl = new MarkupControl(new List<string> { "Hi" }) { BackgroundColor = TestBg };
		var buffer = PaintWithContainer(ctrl);

		Assert.Equal(TestBg, buffer.GetCell(Width - 1, 0).Background);
	}
}
