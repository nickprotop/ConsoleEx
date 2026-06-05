// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

public class ConsoleBufferInvalidateTests
{
	[Fact]
	public void InvalidateFrontBuffer_MakesPopulatedCellsDirtyAgain()
	{
		var buffer = new ConsoleBuffer(10, 3);

		// Paint a cell into the back buffer, then Render() so the front buffer
		// is synced to the back buffer (front == back, nothing dirty).
		buffer.SetNarrowCell(2, 1, 'X', Color.White, Color.Black);
		buffer.Render();

		// After rendering, the painted cell should be in sync (not dirty).
		Assert.Equal(0, buffer.GetDirtyCharacterCount());
		Assert.False(buffer.IsCellDirty(2, 1));

		// Invalidating the front buffer resets it so the next render must
		// re-emit every populated cell.
		buffer.InvalidateFrontBuffer();

		// The previously painted cell is now dirty again.
		Assert.True(buffer.GetDirtyCharacterCount() > 0);
		Assert.True(buffer.IsCellDirty(2, 1));
	}
}
