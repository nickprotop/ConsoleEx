// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.IO;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests;

/// <summary>
/// Layout-focused tests for <see cref="FormControl"/>: the validation-error row must be a clean
/// full-width row (column 0, spanning all form columns), and the form's text editors must stretch
/// to fill their star editor cell (with an optional fixed-width override).
/// </summary>
public class FormLayoutTests
{
	private const int Width = 60;
	private const int Height = 24;

	private static (ConsoleWindowSystem system, Window window) Host(FormControl form)
	{
		Console.SetIn(TextReader.Null);
		var system = TestWindowSystemBuilder.CreateTestSystem(Width, Height);
		var window = new Window(system) { Left = 0, Top = 0, Width = Width, Height = Height };
		window.AddControl(form);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
		return (system, window);
	}

	// ---- ISSUE A: error row must be col 0, spanning all form columns ------------------------------

	[Fact]
	public void Error_SingleField_SpansAllColumns_FromColumnZero()
	{
		var form = new FormControl();
		form.AddText("name", "Name:", required: true);
		Assert.False(form.Validate());

		var placement = form.ErrorPlacementForTest("name");
		Assert.NotNull(placement);
		Assert.Equal(0, placement!.Value.Col);
		Assert.Equal(form.ColumnDefinitions.Count, placement.Value.ColSpan);
	}

	[Fact]
	public void Error_MultiFieldRow_SecondField_StartsAtColumnZero_SpansAllColumns()
	{
		var form = new FormControl();
		// A packed row widens the grid to 4 columns; the second field's label column is 2 (nonzero).
		form.AddRow(
			f => f.AddText("first", "First:", required: true),
			f => f.AddText("last", "Last:", required: true));
		Assert.False(form.Validate());

		int totalColumns = form.ColumnDefinitions.Count;
		Assert.True(totalColumns >= 4, $"packed row should widen the grid (got {totalColumns}).");

		// The second field's error must NOT be indented under its editor column (the pre-fix bug placed
		// it at labelCol=2): it starts at column 0 and spans the full form width.
		var last = form.ErrorPlacementForTest("last");
		Assert.NotNull(last);
		Assert.Equal(0, last!.Value.Col);
		Assert.Equal(totalColumns, last.Value.ColSpan);

		// The first field's error is also a left-anchored full-width row. Its col-span is fixed at
		// placement time to the columns that existed then (the packed row widens progressively as later
		// fields are added — the same documented ordering caveat as WithButtons), so it starts at column 0
		// and spans from the left edge. The key fix is col 0 (no indent), not the exact final span.
		var first = form.ErrorPlacementForTest("first");
		Assert.NotNull(first);
		Assert.Equal(0, first!.Value.Col);
		Assert.True(first.Value.ColSpan >= 2,
			$"the first field's error must start at the left edge spanning at least the base columns (got {first.Value.ColSpan}).");
	}

	// ---- ISSUE B: form text editors stretch to fill the star editor cell -------------------------

	[Fact]
	public void TextEditor_StretchesToFillEditorCell()
	{
		var form = new FormControl();
		form.AddText("host", "Host:", initial: "x"); // short content, wide star column
		var (system, _) = Host(form);

		var prompt = (PromptControl)form.GetEditor("host");

		// The star editor column takes the remaining width after the tiny auto label column. A stretched
		// prompt fills that cell; a content-sized (Left) prompt would be only a few columns wide.
		Assert.True(prompt.ActualWidth > 20,
			$"the form text editor must stretch to fill the wide editor cell (ActualWidth={prompt.ActualWidth}).");

		GC.KeepAlive(system);
	}

	[Fact]
	public void TextEditor_WidthOverride_ProducesFixedWidth_NotStretched()
	{
		var form = new FormControl();
		form.AddText("code", "Code:", width: 8);
		var (system, _) = Host(form);

		var prompt = (PromptControl)form.GetEditor("code");

		// Explicit width wins over fill: the input area is fixed at 8, so the arranged width is small
		// (label prompt is empty here; input area == 8) — clearly NOT filling the wide editor cell.
		Assert.Equal(8, prompt.InputWidth);
		Assert.True(prompt.ActualWidth <= 12,
			$"an explicit width: override must not stretch (ActualWidth={prompt.ActualWidth}).");

		GC.KeepAlive(system);
	}

	[Fact]
	public void DropdownEditor_AutoFitsToContent_DoesNotStretch()
	{
		var form = new FormControl();
		form.AddDropdown("driver", "Driver:", new[] { "a", "b" }, initial: "a");
		var (system, _) = Host(form);

		var dropdown = (DropdownControl)form.GetEditor("driver");

		// A dropdown AUTO-FITS to content (Left alignment) rather than stretching: stretching would flush
		// the ▾ arrow to the far cell edge with a dead gap after the value. So even in a wide star cell the
		// dropdown stays only as wide as its selected value plus the arrow — a handful of columns, not the
		// full cell width.
		Assert.True(dropdown.ActualWidth < 20,
			$"the form dropdown editor must auto-fit to content, not stretch the wide cell (ActualWidth={dropdown.ActualWidth}).");

		GC.KeepAlive(system);
	}

	[Fact]
	public void DropdownEditor_WidthOverride_ProducesFixedWidth_NotStretched()
	{
		var form = new FormControl();
		form.AddDropdown("driver", "Driver:", new[] { "a", "b" }, initial: "a", width: 10);
		var (system, _) = Host(form);

		var dropdown = (DropdownControl)form.GetEditor("driver");

		Assert.Equal(10, dropdown.Width);
		Assert.True(dropdown.ActualWidth <= 12,
			$"an explicit width: override must not stretch the dropdown (ActualWidth={dropdown.ActualWidth}).");

		GC.KeepAlive(system);
	}

	[Fact]
	public void SliderEditor_StretchesToFillEditorCell()
	{
		var form = new FormControl();
		form.AddSlider("level", "Level:", 0, 100, 50);
		var (system, _) = Host(form);

		var slider = (SliderControl)form.GetEditor("level");

		Assert.True(slider.ActualWidth > 20,
			$"the form slider editor must stretch to fill the wide editor cell (ActualWidth={slider.ActualWidth}).");

		GC.KeepAlive(system);
	}

	[Fact]
	public void SliderEditor_WidthOverride_ProducesFixedWidth_NotStretched()
	{
		var form = new FormControl();
		form.AddSlider("level", "Level:", 0, 100, 50, width: 12);
		var (system, _) = Host(form);

		var slider = (SliderControl)form.GetEditor("level");

		Assert.Equal(12, slider.Width);
		Assert.True(slider.ActualWidth <= 14,
			$"an explicit width: override must not stretch the slider (ActualWidth={slider.ActualWidth}).");

		GC.KeepAlive(system);
	}

	// ---- FormXml width= attribute wiring ---------------------------------------------------------

	[Fact]
	public void FormXml_TextWidth_ProducesFixedWidthEditor()
	{
		var form = SharpConsoleUI.Controls.Forms.FormXml.FromXml(
			"<form><text name='code' label='Code:' width='8'/></form>");

		var prompt = (PromptControl)form.GetEditor("code");
		Assert.Equal(8, prompt.InputWidth);
	}

	[Fact]
	public void FormXml_DropdownWidth_ProducesFixedWidthEditor()
	{
		var form = SharpConsoleUI.Controls.Forms.FormXml.FromXml(
			"<form><dropdown name='driver' label='Driver:' options='a,b' width='9'/></form>");

		var dropdown = (DropdownControl)form.GetEditor("driver");
		Assert.Equal(9, dropdown.Width);
	}

	[Fact]
	public void FormXml_TextWithoutWidth_Stretches()
	{
		var form = SharpConsoleUI.Controls.Forms.FormXml.FromXml(
			"<form><text name='host' label='Host:' initial='x'/></form>");
		var (system, _) = Host(form);

		var prompt = (PromptControl)form.GetEditor("host");

		// No width= → the editor stretches to fill the wide star editor cell.
		Assert.Null(prompt.InputWidth);
		Assert.True(prompt.ActualWidth > 20,
			$"a <text> without width= must stretch to fill the editor cell (ActualWidth={prompt.ActualWidth}).");

		GC.KeepAlive(system);
	}

	[Fact]
	public void FormXml_MalformedWidth_Throws()
	{
		Assert.Throws<SharpConsoleUI.Controls.Forms.FormXmlException>(() =>
			SharpConsoleUI.Controls.Forms.FormXml.FromXml(
				"<form><text name='code' label='Code:' width='abc'/></form>"));
	}

	// ---- per-field align= override ---------------------------------------------------------------

	[Fact]
	public void TextEditor_AlignLeftOverride_DoesNotStretch()
	{
		var form = new FormControl();
		// The default is Stretch (fills the wide star cell); an explicit align: Left must override it so the
		// editor stays content-sized rather than filling.
		form.AddText("host", "Host:", initial: "x", align: SharpConsoleUI.Layout.HorizontalAlignment.Left);
		var (system, _) = Host(form);

		var prompt = (PromptControl)form.GetEditor("host");

		Assert.Equal(SharpConsoleUI.Layout.HorizontalAlignment.Left, prompt.HorizontalAlignment);
		Assert.True(prompt.ActualWidth < 20,
			$"align: Left must override the stretch default so the editor is content-sized (ActualWidth={prompt.ActualWidth}).");

		GC.KeepAlive(system);
	}

	[Fact]
	public void DropdownEditor_AlignStretchOverride_FillsCell()
	{
		var form = new FormControl();
		// The default is auto-fit (Left); an explicit align: Stretch must override it so the dropdown fills
		// the wide star cell.
		form.AddDropdown("driver", "Driver:", new[] { "a", "b" }, initial: "a",
			align: SharpConsoleUI.Layout.HorizontalAlignment.Stretch);
		var (system, _) = Host(form);

		var dropdown = (DropdownControl)form.GetEditor("driver");

		Assert.Equal(SharpConsoleUI.Layout.HorizontalAlignment.Stretch, dropdown.HorizontalAlignment);
		Assert.True(dropdown.ActualWidth > 20,
			$"align: Stretch must override the auto-fit default so the dropdown fills the wide cell (ActualWidth={dropdown.ActualWidth}).");

		GC.KeepAlive(system);
	}

	[Fact]
	public void FormXml_TextAlignRight_ProducesRightAlignedEditor()
	{
		var form = SharpConsoleUI.Controls.Forms.FormXml.FromXml(
			"<form><text name='code' label='Code:' align='right'/></form>");

		var prompt = (PromptControl)form.GetEditor("code");
		Assert.Equal(SharpConsoleUI.Layout.HorizontalAlignment.Right, prompt.HorizontalAlignment);
	}

	[Fact]
	public void FormXml_DropdownAlignStretch_ProducesStretchedEditor()
	{
		var form = SharpConsoleUI.Controls.Forms.FormXml.FromXml(
			"<form><dropdown name='driver' label='Driver:' options='a,b' align='stretch'/></form>");

		var dropdown = (DropdownControl)form.GetEditor("driver");
		Assert.Equal(SharpConsoleUI.Layout.HorizontalAlignment.Stretch, dropdown.HorizontalAlignment);
	}

	[Fact]
	public void FormXml_InvalidAlign_Throws()
	{
		Assert.Throws<SharpConsoleUI.Controls.Forms.FormXmlException>(() =>
			SharpConsoleUI.Controls.Forms.FormXml.FromXml(
				"<form><text name='code' label='Code:' align='nope'/></form>"));
	}
}
