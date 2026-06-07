using System.Drawing;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

public class TryGetCellTests
{
	[Fact]
	public void TryGetCell_ReturnsWrittenCell()
	{
		var buffer = new ConsoleBuffer(10, 3);
		buffer.SetNarrowCell(4, 1, 'Z', Color.White, Color.Black);

		Assert.True(buffer.TryGetCell(4, 1, out var ch, out var fg, out var bg));
		Assert.Equal('Z', ch);
		Assert.Equal(Color.White, fg);
		Assert.Equal(Color.Black, bg);

		Assert.False(buffer.TryGetCell(-1, 0, out _, out _, out _));
	}
}
