// -----------------------------------------------------------------------
// ConsoleEx - GridControl Auto-track margin double-subtraction regression
// -----------------------------------------------------------------------
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using CB = SharpConsoleUI.Builders.Controls;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

public class GridAutoMarginMeasureTests
{
	// An Auto column whose content is a Markup WITH a horizontal margin must size to the content's
	// FULL width (incl. its margin) and NOT force the content to wrap. Previously the grid subtracted
	// the child margin to compute the pass-2 re-measure width, and the control's own MeasureDOM
	// subtracted the margin AGAIN — so content got (colWidth - 2*margin) and a single-line label
	// like "Animation" wrapped to two lines.
	[Fact]
	public void AutoColumn_MarginedMarkup_DoesNotWrap()
	{
		var label = CB.Markup("Animation").WithMargin(1, 0, 1, 0).Build();
		var grid = CB.Grid()
			.Columns(GridLength.Auto(), GridLength.Star(1))
			.Rows(GridLength.Star(1))
			.Place(label, 0, 0)
			.Place(CB.Markup("value").Build(), 0, 1)
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var system = TestWindowSystemBuilder.CreateTestSystem(60, 10);
		var window = new Window(system) { Title = "T", Left = 0, Top = 0, Width = 60, Height = 10 };
		window.AddControl(grid);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// "Animation" is 9 columns; on one line the label height stays 1. A wrapped label is 2+ rows.
		Assert.Equal(1, label.ActualHeight);
	}
}
