// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing
{
	/// <summary>
	/// Named colors must carry the xterm-256 value their name denotes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The markup palette is the xterm-256 list, and a name in it is a promise
	/// about a specific slot. The list repeats 48 of its names across 102 of
	/// its 256 slots, so a name alone does not say which slot is meant, and the
	/// duplicates are where a wrong value hides: the colors are plausible and
	/// adjacent, so nothing looks broken on screen.
	/// </para>
	/// <para>
	/// Three got copied from a neighbour rather than read from the list. Each
	/// case below is the value the name denotes in xterm-256, so a regression
	/// names the slot it broke instead of only reporting two colors differing.
	/// </para>
	/// </remarks>
	public class ColorTableXtermFidelityTests
	{
		[Theory]
		// Slot 139 is a mauve, not a grey. The name invites #a0a0a0, which is
		// why the wrong value survived: it reads as a grey ramp entry and the
		// ramp around it is correct.
		[InlineData("grey63", 175, 135, 175)]
		[InlineData("gray63", 175, 135, 175)]
		public void NamedGrey_CarriesItsXtermValue(string name, byte r, byte g, byte b)
		{
			Assert.True(Color.TryFromName(name, out var color), $"'{name}' is not a known color name");
			Assert.Equal(new Color(r, g, b), color);
		}

		[Theory]
		// Second occurrences of a repeated name. Each must differ from the
		// first occurrence; a duplicated value means one was copied.
		[InlineData("gold31", 215, 175, 0)]           // xterm 178, vs gold3 at 142
		[InlineData("darkolivegreen11", 215, 255, 135)] // xterm 192, vs darkolivegreen1 at 191
		public void RepeatedName_SecondOccurrence_IsItsOwnSlot(string name, byte r, byte g, byte b)
		{
			Assert.True(Color.TryFromName(name, out var color), $"'{name}' is not a known color name");
			Assert.Equal(new Color(r, g, b), color);
		}

		[Fact]
		public void IndianRedSecondOccurrence_IsReachableByName()
		{
			// xterm 167 (#d75f5f) sits between indianred (131) and
			// indianred1 (203). Color.IndianRed_1 holds it, but no table name
			// reached it, so the color was unusable from markup.
			//
			// Underscores are stripped before lookup, so Spectre's spelling
			// "indianred_1" normalizes to "indianred1", which is already the
			// distinct xterm name IndianRed (203). xterm 167 therefore cannot
			// use the usual occurrence suffix and is named "indianred2".
			Assert.True(Color.TryFromName("indianred2", out var color),
				"xterm 167 has no name in the color table");
			Assert.Equal(new Color(215, 95, 95), color);

			// The collision itself stands: asking by Spectre's spelling still
			// lands on 203. Pinned so the behaviour is deliberate, not a
			// surprise discovered later.
			Assert.True(Color.TryFromName("indianred_1", out var collided));
			Assert.Equal(new Color(255, 95, 95), collided);
		}

		[Theory]
		// A repeated name and its later occurrences must never share a value.
		[InlineData("gold3", "gold31")]
		[InlineData("darkolivegreen1", "darkolivegreen11")]
		[InlineData("purple", "purple1")]
		[InlineData("blue3", "blue31")]
		public void RepeatedName_OccurrencesDiffer(string first, string later)
		{
			Assert.True(Color.TryFromName(first, out var a), $"'{first}' unknown");
			Assert.True(Color.TryFromName(later, out var b), $"'{later}' unknown");
			Assert.NotEqual(a, b);
		}
	}
}
