using Xunit;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using System.Text;

namespace SharpConsoleUI.Tests.Parsing
{
	public class CellTests
	{
		[Fact]
		public void Constructor_SetsCharacterForegroundBackground()
		{
			var cell = new Cell('A', Color.Red, Color.Blue);
			Assert.Equal(new Rune('A'), cell.Character);
			Assert.Equal(Color.Red, cell.Foreground);
			Assert.Equal(Color.Blue, cell.Background);
			Assert.Equal(TextDecoration.None, cell.Decorations);
			Assert.True(cell.Dirty);
		}

		[Fact]
		public void Constructor_WithDecorations_SetsAll()
		{
			var cell = new Cell('B', Color.Green, Color.White, TextDecoration.Bold);
			Assert.Equal(new Rune('B'), cell.Character);
			Assert.Equal(Color.Green, cell.Foreground);
			Assert.Equal(Color.White, cell.Background);
			Assert.Equal(TextDecoration.Bold, cell.Decorations);
			Assert.True(cell.Dirty);
		}

		[Fact]
		public void Blank_IsSpaceWithWhiteFgBlackBg()
		{
			var blank = Cell.Blank;
			Assert.Equal(new Rune(' '), blank.Character);
			Assert.Equal(Color.White, blank.Foreground);
			Assert.Equal(Color.Black, blank.Background);
		}

		[Fact]
		public void BlankWithBackground_SetsGivenBg()
		{
			var cell = Cell.BlankWithBackground(Color.Blue);
			Assert.Equal(new Rune(' '), cell.Character);
			Assert.Equal(Color.White, cell.Foreground);
			Assert.Equal(Color.Blue, cell.Background);
		}

		[Fact]
		public void Create_SetsFields()
		{
			var cell = Cell.Create('X', Color.Yellow, Color.Navy);
			Assert.Equal(new Rune('X'), cell.Character);
			Assert.Equal(Color.Yellow, cell.Foreground);
			Assert.Equal(Color.Navy, cell.Background);
		}

		[Fact]
		public void VisuallyEquals_IgnoresDirtyFlag()
		{
			var a = new Cell('A', Color.Red, Color.Black);
			var b = a.AsClean();
			Assert.True(a.VisuallyEquals(b));
			Assert.NotEqual(a.Dirty, b.Dirty);
		}

		[Fact]
		public void VisuallyEquals_DifferentChar_ReturnsFalse()
		{
			var a = new Cell('A', Color.Red, Color.Black);
			var b = new Cell('B', Color.Red, Color.Black);
			Assert.False(a.VisuallyEquals(b));
		}

		[Fact]
		public void Equals_IncludesDirtyFlag()
		{
			var a = new Cell('A', Color.Red, Color.Black);
			var b = a.AsClean();
			Assert.False(a.Equals(b)); // Different Dirty
		}

		[Fact]
		public void Equals_SameDirty_ReturnsTrue()
		{
			var a = new Cell('A', Color.Red, Color.Black);
			var b = new Cell('A', Color.Red, Color.Black);
			Assert.True(a.Equals(b)); // Both dirty=true from constructor
		}

		[Fact]
		public void AsClean_ReturnsCopyWithDirtyFalse()
		{
			var cell = new Cell('A', Color.Red, Color.Black);
			Assert.True(cell.Dirty);
			var clean = cell.AsClean();
			Assert.False(clean.Dirty);
			Assert.Equal(cell.Character, clean.Character);
			Assert.Equal(cell.Foreground, clean.Foreground);
		}

		[Fact]
		public void AsDirty_ReturnsCopyWithDirtyTrue()
		{
			var cell = new Cell('A', Color.Red, Color.Black).AsClean();
			Assert.False(cell.Dirty);
			var dirty = cell.AsDirty();
			Assert.True(dirty.Dirty);
		}

		[Fact]
		public void OperatorEqual_SameCells_True()
		{
			var a = new Cell('A', Color.Red, Color.Black);
			var b = new Cell('A', Color.Red, Color.Black);
			Assert.True(a == b);
		}

		[Fact]
		public void OperatorNotEqual_DifferentCells_True()
		{
			var a = new Cell('A', Color.Red, Color.Black);
			var b = new Cell('B', Color.Red, Color.Black);
			Assert.True(a != b);
		}

		[Fact]
		public void DefaultCell_ZeroInitialized()
		{
			var cell = default(Cell);
			Assert.Equal(new Rune('\0'), cell.Character);
			Assert.Equal(default(Color), cell.Foreground);
			Assert.Equal(default(Color), cell.Background);
			Assert.Equal(TextDecoration.None, cell.Decorations);
			Assert.False(cell.Dirty);
		}

		[Fact]
		public void GetHashCode_EqualCells_SameHash()
		{
			var a = new Cell('A', Color.Red, Color.Black);
			var b = new Cell('A', Color.Red, Color.Black);
			Assert.Equal(a.GetHashCode(), b.GetHashCode());
		}

		[Fact]
		public void Equals_WithObject_Works()
		{
			var a = new Cell('A', Color.Red, Color.Black);
			object b = new Cell('A', Color.Red, Color.Black);
			Assert.True(a.Equals(b));
		}

		[Fact]
		public void Equals_WithNonCellObject_ReturnsFalse()
		{
			var a = new Cell('A', Color.Red, Color.Black);
			Assert.False(a.Equals("not a cell"));
		}
	}
}
