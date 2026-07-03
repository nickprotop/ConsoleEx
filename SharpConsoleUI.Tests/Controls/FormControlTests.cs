// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests;

public class FormControlTests
{
	private enum Size { Small, Large }

	[Fact]
	public void AddText_And_GetValues_ReturnsTypedValue()
	{
		var form = new FormControl();
		form.AddText("host", "Host:", initial: "localhost");
		Assert.Equal("localhost", form.GetValues()["host"]);
	}

	[Fact]
	public void AddCheckbox_ReturnsBoolAsString()
	{
		var form = new FormControl();
		form.AddCheckbox("ssl", "SSL", initial: true);
		Assert.Equal("true", form.GetValues()["ssl"]);
	}

	[Fact]
	public void AddRadio_String_SelectionFlowsToValues()
	{
		var form = new FormControl();
		form.AddRadio("size", "Size:", "Small", "Large");
		// select via the typed group editor
		var g = (RadioGroup<string>)form.GetEditor("size");
		g.SelectedValue = "Large";
		Assert.Equal("Large", form.GetValues()["size"]);
	}

	[Fact]
	public void AddField_EscapeHatch_InvokesGetter()
	{
		var form = new FormControl();
		var custom = new MarkupControl(new List<string> { "x" });
		form.AddField("k", "K:", custom, () => "customValue");
		Assert.Equal("customValue", form.GetValues()["k"]);
	}

	[Fact]
	public void GetEditor_ReturnsTheEditorControl()
	{
		var form = new FormControl();
		form.AddText("host", "Host:");
		Assert.IsType<PromptControl>(form.GetEditor("host"));
	}

	[Fact]
	public void Required_Empty_FailsValidation_ShowsError()
	{
		var form = new FormControl();
		form.AddText("name", "Name:", required: true);
		Assert.False(form.Validate());
		Assert.True(form.HasErrorForTest("name"));   // internal seam: error row visible w/ text
		((PromptControl)form.GetEditor("name")).Input = "x";
		Assert.True(form.Validate());
		Assert.False(form.HasErrorForTest("name"));
	}

	[Fact]
	public void CustomValidator_ShowsMessage()
	{
		var form = new FormControl();
		form.AddText("port", "Port:", validate: v => int.TryParse(v, out _) ? null : "must be a number");
		((PromptControl)form.GetEditor("port")).Input = "abc";
		Assert.False(form.Validate());
		Assert.Equal("must be a number", form.ErrorTextForTest("port"));
	}

	[Fact]
	public void Submit_Invalid_DoesNotFire_Valid_FiresWithValues()
	{
		var form = new FormControl();
		form.AddText("name", "Name:", required: true);
		IReadOnlyDictionary<string, string?>? got = null;
		form.Submitted += (_, v) => got = v;
		form.Submit();
		Assert.Null(got);                              // invalid → no fire
		((PromptControl)form.GetEditor("name")).Input = "Alice";
		form.Submit();
		Assert.NotNull(got);
		Assert.Equal("Alice", got!["name"]);
	}

	[Fact]
	public void Section_Collapsible_HidesAndShowsItsFieldRows()
	{
		var form = new FormControl();
		form.AddText("a", "A:");
		form.AddSection("Advanced", collapsible: true, startCollapsed: true);
		form.AddText("b", "B:");   // belongs to Advanced
		Assert.False(((IWindowControl)form.GetEditor("b")).Visible);   // startCollapsed → hidden
		Assert.True(((IWindowControl)form.GetEditor("a")).Visible);    // outside section → visible
		form.ToggleSectionForTest("Advanced");                          // internal seam simulating the toggle button
		Assert.True(((IWindowControl)form.GetEditor("b")).Visible);    // expanded → shown
	}

	[Fact]
	public void CollapsedSection_StillReportsValues()
	{
		var form = new FormControl();
		form.AddSection("Advanced", collapsible: true, startCollapsed: true);
		form.AddText("b", "B:", initial: "kept");
		Assert.Equal("kept", form.GetValues()["b"]);        // hidden fields still report
	}

	[Fact]
	public void CollapsedSection_ValidationError_StaysHiddenUntilExpanded()
	{
		var form = new FormControl();
		form.AddSection("Advanced", collapsible: true, startCollapsed: true);
		form.AddText("key", "Key:", required: true);   // required field inside a collapsed section

		Assert.False(form.Validate());                 // required field is empty → form invalid
													   // The field is hidden (collapsed section), so its error row must NOT float over the header.
		Assert.False(form.GetLabelForTest("key").Visible);
		Assert.False(form.HasErrorForTest("key"));     // error row stays hidden while the field is hidden

		form.ToggleSectionForTest("Advanced");         // expand → the pending error becomes visible
		Assert.True(form.HasErrorForTest("key"));
	}

	[Fact]
	public void Hint_RendersUnderField_DistinctFromError()
	{
		var form = new FormControl();
		form.AddText("port", "Port:", hint: "blank = default");
		Assert.Equal("blank = default", form.HintTextForTest("port"));
		Assert.True(form.HintVisibleForTest("port"));       // hint always visible
	}

	[Fact]
	public void AddRow_PacksFieldsAndBothReportValues()
	{
		var form = new FormControl();
		form.AddRow(f => f.AddText("first", "First:", initial: "A"),
					f => f.AddText("last", "Last:", initial: "B"));
		Assert.Equal("A", form.GetValues()["first"]);
		Assert.Equal("B", form.GetValues()["last"]);
		Assert.Equal(1, form.RowGroupCountForTest());       // both on one logical row-group
	}

	[Fact]
	public void WithButtons_OkTriggersSubmit()
	{
		var form = new FormControl();
		form.AddText("name", "Name:", initial: "Alice");
		form.WithButtons();
		bool submitted = false;
		form.Submitted += (_, __) => submitted = true;
		form.ClickOkForTest();                               // internal seam invoking the OK button's click
		Assert.True(submitted);
	}
}
