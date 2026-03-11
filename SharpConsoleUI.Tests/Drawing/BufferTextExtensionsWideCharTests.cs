using Xunit;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Drawing;
using System.Text;

namespace SharpConsoleUI.Tests.Drawing
{
	public class BufferTextExtensionsWideCharTests
	{
		[Fact]
		public void WriteStringCentered_CjkText_CenteredByDisplayWidth()
		{
			// "中文" = 2 chars, each width 2 = 4 display columns
			// In a 10-wide buffer, centered: (10 - 4) / 2 = 3 -> starts at column 3
			var buffer = new CharacterBuffer(10, 5);
			string cjk = "\u4E2D\u6587"; // 中文

			buffer.WriteStringCentered(0, cjk, Color.White, Color.Black);

			Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(3, 0).Character);
			// Column 4 is the wide continuation of 中
			Assert.Equal(new Rune('\u6587'), buffer.GetCell(5, 0).Character);
			// Column 6 is the wide continuation of 文
		}

		[Fact]
		public void WriteStringCentered_MixedText_CenteredCorrectly()
		{
			// "Hi中文" = H(1) + i(1) + 中(2) + 文(2) = 6 display columns
			// In a 12-wide buffer, centered: (12 - 6) / 2 = 3 -> starts at column 3
			var buffer = new CharacterBuffer(12, 5);
			string mixed = "Hi\u4E2D\u6587"; // Hi中文

			buffer.WriteStringCentered(0, mixed, Color.White, Color.Black);

			Assert.Equal(new Rune('H'), buffer.GetCell(3, 0).Character);
			Assert.Equal(new Rune('i'), buffer.GetCell(4, 0).Character);
			Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(5, 0).Character);
			// Column 6 is continuation of 中
			Assert.Equal(new Rune('\u6587'), buffer.GetCell(7, 0).Character);
			// Column 8 is continuation of 文
		}

		[Fact]
		public void WriteStringRight_CjkText_AlignedByDisplayWidth()
		{
			// "中文" = 4 display columns
			// In a 10-wide buffer, right-aligned: 10 - 4 = 6 -> starts at column 6
			var buffer = new CharacterBuffer(10, 5);
			string cjk = "\u4E2D\u6587"; // 中文

			buffer.WriteStringRight(0, cjk, Color.White, Color.Black);

			Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(6, 0).Character);
			// Column 7 is continuation of 中
			Assert.Equal(new Rune('\u6587'), buffer.GetCell(8, 0).Character);
			// Column 9 is continuation of 文
		}

		[Fact]
		public void WriteStringRight_MixedText_AlignedCorrectly()
		{
			// "AB中" = A(1) + B(1) + 中(2) = 4 display columns
			// In a 10-wide buffer, right-aligned: 10 - 4 = 6 -> starts at column 6
			var buffer = new CharacterBuffer(10, 5);
			string mixed = "AB\u4E2D"; // AB中

			buffer.WriteStringRight(0, mixed, Color.White, Color.Black);

			Assert.Equal(new Rune('A'), buffer.GetCell(6, 0).Character);
			Assert.Equal(new Rune('B'), buffer.GetCell(7, 0).Character);
			Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(8, 0).Character);
			// Column 9 is continuation of 中
		}

		[Fact]
		public void WrapText_CjkText_WrapsAtDisplayWidth()
		{
			// "中文字体" = 4 chars, each width 2 = 8 display columns
			// With width=4, each line fits 2 CJK chars (4 columns)
			// Should wrap into 2 lines: "中文" and "字体"
			var buffer = new CharacterBuffer(20, 5);
			string cjk = "\u4E2D\u6587\u5B57\u4F53"; // 中文字体

			buffer.WriteWrappedText(0, 0, 4, cjk, Color.White, Color.Black);

			// First line: 中文
			Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(0, 0).Character);
			Assert.Equal(new Rune('\u6587'), buffer.GetCell(2, 0).Character);

			// Second line: 字体
			Assert.Equal(new Rune('\u5B57'), buffer.GetCell(0, 1).Character);
			Assert.Equal(new Rune('\u4F53'), buffer.GetCell(2, 1).Character);
		}

		[Fact]
		public void WrapText_MixedText_WideCharDoesntSplitAcrossLines()
		{
			// With width=5, "Hello中文" should wrap:
			// "Hello" (5 cols) on line 0, "中文" (4 cols) on line 1
			// The wide char 中 should not be split across lines
			var buffer = new CharacterBuffer(20, 5);
			string mixed = "Hello \u4E2D\u6587"; // "Hello 中文"

			buffer.WriteWrappedText(0, 0, 5, mixed, Color.White, Color.Black);

			// First line: "Hello"
			Assert.Equal(new Rune('H'), buffer.GetCell(0, 0).Character);
			Assert.Equal(new Rune('o'), buffer.GetCell(4, 0).Character);

			// Second line: "中文" - wide chars should not be split
			Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(0, 1).Character);
			Assert.Equal(new Rune('\u6587'), buffer.GetCell(2, 1).Character);
		}
	}
}
