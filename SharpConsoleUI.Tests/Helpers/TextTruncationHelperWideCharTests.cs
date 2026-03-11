using Xunit;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Tests.Helpers
{
	public class TextTruncationHelperWideCharTests
	{
		[Fact]
		public void Truncate_CjkText_TruncatesByDisplayWidth()
		{
			// "中文字" = 3 CJK chars, each width 2 = 6 display columns
			// maxWidth=5: ellipsis "..." = 3 cols, available for content = 2
			// availableForContent (2) < MinContentCharsBeforeEllipsis (3),
			// so falls back to MarkupParser.Truncate(text, 5)
			// which fits "中文" (4 cols) but not "中文字" (6 cols), yielding "中文"
			string cjk = "\u4E2D\u6587\u5B57"; // 中文字

			string result = TextTruncationHelper.Truncate(cjk, 5);

			// Should be truncated (original is 6 cols, maxWidth is 5)
			int resultWidth = MarkupParser.StripLength(result);
			Assert.True(resultWidth <= 5, $"Result width {resultWidth} exceeds maxWidth 5");
			Assert.True(result.Length < cjk.Length || result != cjk,
				"Result should be truncated");
		}

		[Fact]
		public void Truncate_WideCharAtBoundary_TruncatesBefore()
		{
			// "A中文" = A(1) + 中(2) + 文(2) = 5 display columns
			// maxWidth=4: ellipsis "..." = 3 cols, available for content = 1
			// availableForContent (1) < MinContentCharsBeforeEllipsis (3),
			// so falls back to MarkupParser.Truncate(text, 4) which gives "A中" (3 cols)
			// The wide char 文 should not be partially included
			string mixed = "A\u4E2D\u6587"; // A中文

			string result = TextTruncationHelper.Truncate(mixed, 4);

			int resultWidth = MarkupParser.StripLength(result);
			Assert.True(resultWidth <= 4, $"Result width {resultWidth} exceeds maxWidth 4");
			// The result should not split a wide character
		}

		[Fact]
		public void Truncate_MixedText_CorrectEllipsisPosition()
		{
			// "Hello中文世界" = 5 + 8 = 13 display columns
			// maxWidth=10: ellipsis "..." = 3, available for content = 7
			// Truncated content should be 7 display cols + "..."
			string mixed = "Hello\u4E2D\u6587\u4E16\u754C"; // Hello中文世界

			string result = TextTruncationHelper.Truncate(mixed, 10);

			int resultWidth = MarkupParser.StripLength(result);
			Assert.True(resultWidth <= 10, $"Result width {resultWidth} exceeds maxWidth 10");
			Assert.Contains("...", result);
		}

		[Fact]
		public void Truncate_ExactFit_NoTruncation()
		{
			// "中文" = 4 display columns, maxWidth=4 -> exact fit, no truncation
			string cjk = "\u4E2D\u6587"; // 中文

			string result = TextTruncationHelper.Truncate(cjk, 4);

			Assert.Equal(cjk, result);
		}

		[Fact]
		public void Truncate_OneLessThanWide_Truncates()
		{
			// "中文" = 4 display columns, maxWidth=3 -> must truncate
			string cjk = "\u4E2D\u6587"; // 中文

			string result = TextTruncationHelper.Truncate(cjk, 3);

			int resultWidth = MarkupParser.StripLength(result);
			Assert.True(resultWidth <= 3, $"Result width {resultWidth} exceeds maxWidth 3");
			Assert.NotEqual(cjk, result);
		}
	}
}
