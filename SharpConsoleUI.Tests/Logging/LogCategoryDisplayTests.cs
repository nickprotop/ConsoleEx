// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Logging;
using Xunit;
using OurLevel = SharpConsoleUI.Logging.LogLevel;

namespace SharpConsoleUI.Tests.Logging;

/// <summary>
/// How a logger category is fitted into the log viewer's category column.
/// </summary>
/// <remarks>
/// The table painter truncates a cell from the LEFT, keeping the head, which is right for a
/// general cell and wrong for a dotted logger category: a <c>Microsoft.Extensions.Logging</c>
/// category is a full type name, whose head is namespace noise and whose tail is the type the
/// reader is looking for.
/// </remarks>
public class LogCategoryDisplayTests
{
	private static int Width => ControlDefaults.LogViewerCategoryColumnWidth;

	[Fact]
	public void ShortCategory_IsUnchanged()
	{
		// The library's own categories are bare words and always fit.
		Assert.Equal("Focus", LogTableDataSource.CategoryText("Focus"));
		Assert.Equal("Window", LogTableDataSource.CategoryText("Window"));
		Assert.Equal("Render", LogTableDataSource.CategoryText("Render"));
	}

	[Fact]
	public void NullOrEmpty_BecomesEmpty()
	{
		Assert.Equal(string.Empty, LogTableDataSource.CategoryText(null));
		Assert.Equal(string.Empty, LogTableDataSource.CategoryText(string.Empty));
	}

	[Fact]
	public void DottedTypeName_KeepsTheLastSegment()
	{
		Assert.Equal("OrderProcessor", LogTableDataSource.CategoryText("MyApp.Services.OrderProcessor"));
	}

	[Fact]
	public void DottedName_KeepsTheLongestTrailingRunThatFits()
	{
		// "Services.Orders" is 15 columns, one over the 14-wide column, so the next
		// segment boundary in is the one that fits.
		var text = LogTableDataSource.CategoryText("MyApp.Services.Orders");
		Assert.Equal("Orders", text);
		Assert.True(UnicodeWidth.GetStringWidth(text) <= Width);
	}

	[Fact]
	public void DottedName_ThatAlreadyFits_IsNotShortened()
	{
		// 13 columns: under the limit, so no segment is dropped even though it has a dot.
		Assert.Equal("App.Orders", LogTableDataSource.CategoryText("App.Orders"));
	}

	[Fact]
	public void SingleSegmentTooLong_KeepsItsTail()
	{
		var text = LogTableDataSource.CategoryText("A.B.VeryLongSingleSegmentNameThatCannotFit");

		Assert.Equal(Width, UnicodeWidth.GetStringWidth(text));
		Assert.EndsWith("ThatCannotFit", text);
	}

	[Fact]
	public void NoDots_AndTooLong_KeepsItsTail()
	{
		var text = LogTableDataSource.CategoryText("ThisIsOneVeryLongWordWithNoDots");

		Assert.Equal(Width, UnicodeWidth.GetStringWidth(text));
		Assert.EndsWith("WithNoDots", text);
	}

	[Fact]
	public void TrailingDot_DoesNotProduceAnEmptyResult()
	{
		// Defensive: a category ending in '.' would leave an empty last segment. Whatever the
		// result is, it must not be empty while the input was not.
		var text = LogTableDataSource.CategoryText("MyApp.Services.SomethingLong.");
		Assert.False(string.IsNullOrEmpty(text));
		Assert.True(UnicodeWidth.GetStringWidth(text) <= Width);
	}

	[Fact]
	public void WideCharacters_AreMeasuredInColumnsNotCodeUnits()
	{
		// CJK characters are two columns each, so 10 of them exceed a 14-column budget even
		// though the string's Length is 10.
		var text = LogTableDataSource.CategoryText("日本語日本語日本語日");

		Assert.True(UnicodeWidth.GetStringWidth(text) <= Width,
			$"'{text}' is {UnicodeWidth.GetStringWidth(text)} columns, over the {Width} limit");
	}

	[Fact]
	public void ResultAlwaysFitsTheColumn()
	{
		var cases = new[]
		{
			"Focus",
			"MyApp.Services.OrderProcessor",
			"A.B.C.D.E.F.G.VeryLongTrailingSegmentIndeed",
			"NoDotsButExtremelyLongAllTheSame",
			"Microsoft.Extensions.Hosting.Internal.ApplicationLifetime",
			"x",
		};

		foreach (var c in cases)
		{
			var text = LogTableDataSource.CategoryText(c);
			Assert.True(UnicodeWidth.GetStringWidth(text) <= Width,
				$"'{c}' produced '{text}' at {UnicodeWidth.GetStringWidth(text)} columns");
		}
	}

	/// <summary>
	/// Only the DISPLAYED text is shortened. The stored entry keeps the full category, which is
	/// what the category filter and the search read, so a full type name stays matchable.
	/// </summary>
	[Fact]
	public void StoredEntryKeepsTheFullCategory()
	{
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var logger = new SharpConsoleUILoggerProvider(svc).CreateLogger("MyApp.Services.OrderProcessor");

		logger.LogInformation("m");

		Assert.Equal("MyApp.Services.OrderProcessor", Assert.Single(svc.GetAllLogs()).Category);
	}
}
