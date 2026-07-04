// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers
{
	public class ControlRenderingHelpersFillRectTests
	{
		[Fact]
		public void FillRect_TransparentBackground_PreservesUnderlyingCharacter()
		{
			var buffer = new CharacterBuffer(4, 1);
			// Seed a distinctive glyph at (0,0).
			buffer.SetNarrowCell(0, 0, '│', new Color(255, 0, 0), new Color(0, 0, 0));

			// Fill that cell with a fully transparent background — must preserve the glyph.
			ControlRenderingHelpers.FillRect(buffer, new LayoutRect(0, 0, 1, 1), new Color(200, 200, 200), Color.Transparent);

			Assert.Equal("│", buffer.GetCell(0, 0).Character.ToString());
		}

		[Fact]
		public void FillRect_OpaqueBackground_StillFillsWithSpace()
		{
			var buffer = new CharacterBuffer(4, 1);
			buffer.SetNarrowCell(0, 0, '│', new Color(255, 0, 0), new Color(0, 0, 0));

			// A non-transparent background fills normally (the cell becomes a space).
			ControlRenderingHelpers.FillRect(buffer, new LayoutRect(0, 0, 1, 1), new Color(200, 200, 200), new Color(10, 20, 30));

			Assert.Equal(" ", buffer.GetCell(0, 0).Character.ToString());
			Assert.Equal(new Color(10, 20, 30), buffer.GetCell(0, 0).Background);
		}
	}
}
