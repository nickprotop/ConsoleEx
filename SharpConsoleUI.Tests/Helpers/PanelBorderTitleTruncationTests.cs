// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// Issue #87: a bordered title wider than its border was dropped entirely and the line filled with
/// horizontals, so a title that fitted on a wide terminal vanished on a narrow one — including the
/// part that would have fitted. It is now truncated with an ellipsis instead.
/// </summary>
public class PanelBorderTitleTruncationTests
{
	private const int Width = 60;
	private static readonly Color Border = Color.White;
	private static readonly Color Bg = Color.Black;

	/// <summary>Draws a top border and returns the rendered row as text.</summary>
	private static string DrawTop(string title, int width = Width,
		TextJustification alignment = TextJustification.Left)
	{
		var buffer = new CharacterBuffer(width, 3);
		var clip = new LayoutRect(0, 0, width, 3);
		PanelBorderRenderer.DrawTopBorder(buffer, 0, 0, width, clip,
			BoxChars.Single, Border, Bg, title, alignment);

		var sb = new System.Text.StringBuilder();
		for (int x = 0; x < width; x++)
			sb.Append(buffer.GetCell(x, 0).Character.ToString());
		return sb.ToString();
	}

	#region The reported bug

	/// <summary>
	/// A title too wide for its border keeps as much as fits and ends in an ellipsis, rather than
	/// disappearing.
	/// </summary>
	/// <remarks>
	/// 80 characters at width 60, not the reporter's 53: their case was a CollapsiblePanel, whose
	/// indicator and chrome eat columns a bare top border does not, so 53 still fits here.
	/// </remarks>
	[Fact]
	public void AnOverlongTitle_IsTruncatedNotDropped()
	{
		string row = DrawTop(new string('b', 80));

		Assert.Contains("b", row);
		Assert.Contains("…", row);
	}

	[Fact]
	public void TheTruncatedRow_IsStillExactlyTheBorderWidth()
	{
		Assert.Equal(Width, DrawTop(new string('b', 80)).Length);
	}

	/// <summary>
	/// The row is still a well-formed border: corners at both ends, and the title still opens with
	/// its leading fence space.
	/// </summary>
	[Fact]
	public void TheTruncatedRow_KeepsItsCornersAndSpacing()
	{
		string row = DrawTop(new string('b', 80));

		Assert.Equal('┌', row[0]);
		Assert.Equal('┐', row[^1]);
		Assert.Contains(" b", row);
	}

	/// <summary>
	/// A truncated title closes the same way a title that fits does: ellipsis, fence space, then at
	/// least one horizontal before the corner. Filling every column up to the corner would leave
	/// the longest titles as the only ones jammed against it.
	/// </summary>
	[Theory]
	[InlineData(57)]
	[InlineData(60)]
	[InlineData(80)]
	[InlineData(200)]
	public void ATruncatedTitle_KeepsAFenceAndAHorizontalBeforeTheCorner(int titleLength)
	{
		string row = DrawTop(new string('b', titleLength));

		Assert.EndsWith("… ─┐", row);
	}

	#endregion

	#region Unchanged behaviour

	/// <summary>A title that fits is drawn exactly as before — no ellipsis, no truncation.</summary>
	[Fact]
	public void ATitleThatFits_IsUnchanged()
	{
		string row = DrawTop("Short title");

		Assert.Contains(" Short title ", row);
		Assert.DoesNotContain("…", row);
	}

	/// <summary>
	/// The widest title that still fits a 60-wide border (2 caps + 2 fencing spaces = 56) is drawn
	/// whole. One more character is the first that truncates — the boundary, pinned from both sides.
	/// </summary>
	[Fact]
	public void TheWidestTitleThatFits_IsNotTruncated()
	{
		string row = DrawTop(new string('a', 56));

		Assert.DoesNotContain("…", row);
		Assert.Contains(new string('a', 56), row);
	}

	[Fact]
	public void OneCharacterWider_IsTheFirstToTruncate()
	{
		string row = DrawTop(new string('a', 57));

		Assert.Contains("…", row);
		Assert.Equal(Width, row.Length);
	}

	[Fact]
	public void NoTitle_DrawsAPlainBorder()
	{
		string row = DrawTop("");

		Assert.DoesNotContain("…", row);
		Assert.Equal(Width, row.Length);
	}

	/// <summary>
	/// A border too narrow to hold even an ellipsis plus its fencing spaces falls back to the plain
	/// line rather than drawing something malformed.
	/// </summary>
	[Fact]
	public void AVeryNarrowBorder_FallsBackToAPlainLine()
	{
		string row = DrawTop("some title", width: 6);

		Assert.Equal(6, row.Length);
	}

	#endregion

	#region Alignment still applies

	[Theory]
	[InlineData(TextJustification.Left)]
	[InlineData(TextJustification.Center)]
	[InlineData(TextJustification.Right)]
	public void TruncationWorksUnderEveryAlignment(TextJustification alignment)
	{
		string row = DrawTop(new string('b', 80), alignment: alignment);

		Assert.Contains("…", row);
		Assert.Equal(Width, row.Length);
	}

	#endregion
}
