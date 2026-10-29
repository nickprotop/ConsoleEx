// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// What the other text controls do with a newline inside one item.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Parsing.MarkupParser.Parse"/> sanitises control characters, a
/// newline among them, to U+FFFD. So any control that hands it a string
/// straight from the caller paints a replacement glyph where the newline was,
/// and a single-row control has no way to show a second line anyway.
/// </para>
/// <para>
/// <see cref="MarkupControl"/> splits on the newline first and is covered by
/// <see cref="MarkupControlMultilineTests"/>. These are the controls that take
/// caller text and were never checked: a glyph is a bug, and what should
/// appear instead is the question each test answers for its own control.
/// </para>
/// </remarks>
public class EmbeddedNewlineAuditTests
{
	/// <summary>The glyph a sanitised control character leaves behind.</summary>
	private static readonly Rune Replacement = new('�');

	/// <summary>One item carrying a newline, the shape issue #45 reported.</summary>
	private const string TwoLines = "alpha\nbeta";

	private static CharacterBuffer Paint(BaseControl control, int w = 40, int h = 12)
	{
		var buffer = new CharacterBuffer(w + 5, h + 3);
		var bounds = new LayoutRect(0, 0, w, h);
		control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return buffer;
	}

	private static bool Shows(CharacterBuffer buf, Rune rune, int w = 40, int h = 12)
	{
		for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
				if (buf.GetCell(x, y).Character == rune)
					return true;
		return false;
	}

	private static string Text(CharacterBuffer buf, int w = 40, int h = 12)
	{
		var sb = new StringBuilder();
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				var ch = buf.GetCell(x, y).Character;
				if (ch.Value != 0) sb.Append(ch.ToString());
			}
			sb.Append('\n');
		}
		return sb.ToString();
	}

	[Fact]
	public void A_list_item_with_a_newline_shows_no_replacement_glyph()
	{
		var list = new ListControl();
		list.AddItem(TwoLines);

		var buffer = Paint(list);

		Assert.Contains("alpha", Text(buffer));
		Assert.False(Shows(buffer, Replacement),
			$"a newline became U+FFFD:\n{Text(buffer)}");
	}

	[Fact]
	public void A_tree_node_with_a_newline_shows_no_replacement_glyph()
	{
		var tree = new TreeControl();
		tree.AddRootNode(new TreeNode(TwoLines));

		var buffer = Paint(tree);

		Assert.Contains("alpha", Text(buffer));
		Assert.False(Shows(buffer, Replacement),
			$"a newline became U+FFFD:\n{Text(buffer)}");
	}

	[Fact]
	public void A_dropdown_item_with_a_newline_shows_no_replacement_glyph()
	{
		var dropdown = new DropdownControl();
		dropdown.AddItem(new DropdownItem(TwoLines));

		var buffer = Paint(dropdown);
		string painted = Text(buffer);

		Assert.False(Shows(buffer, Replacement),
			$"a newline became U+FFFD:\n{painted}");

		// A closed field has one row, so the first line is what it shows - not
		// the two joined, and not a break it has nowhere to put.
		Assert.Contains("alpha", painted);
		Assert.DoesNotContain("beta", painted);
	}

	[Fact]
	public void A_table_cell_with_a_newline_shows_no_replacement_glyph()
	{
		var table = new TableControl();
		table.AddColumn("Col");
		table.AddRow(TwoLines);

		var buffer = Paint(table);

		Assert.Contains("alpha", Text(buffer));
		Assert.False(Shows(buffer, Replacement),
			$"a newline became U+FFFD:\n{Text(buffer)}");
	}
}
