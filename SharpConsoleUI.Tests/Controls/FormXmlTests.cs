// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Controls.Forms;
using Xunit;

namespace SharpConsoleUI.Tests;

public class FormXmlTests
{
	[Fact]
	public void FromXml_TextAndCheckbox_BuildsFields_WithInitialValues()
	{
		var form = FormXml.FromXml(@"
			<form title='Connection'>
			  <text name='host' label='Host:' initial='localhost'/>
			  <checkbox name='ssl' label='Use TLS' initial='true'/>
			</form>");
		var values = form.GetValues();
		Assert.Equal("localhost", values["host"]);
		Assert.Equal("true", values["ssl"]);
	}

	[Fact]
	public void FromXml_NotRootForm_Throws()
	{
		Assert.Throws<FormXmlException>(() => FormXml.FromXml("<nope/>"));
	}

	[Fact]
	public void FromXml_UnknownElement_Throws()
	{
		Assert.Throws<FormXmlException>(() =>
			FormXml.FromXml("<form><frobnicate name='x'/></form>"));
	}

	[Fact]
	public void FromXml_MalformedXml_Throws()
	{
		Assert.Throws<FormXmlException>(() => FormXml.FromXml("<form><text"));
	}

	[Fact]
	public void FromXml_MissingName_Throws()
	{
		Assert.Throws<FormXmlException>(() =>
			FormXml.FromXml("<form><text label='No name'/></form>"));
	}

	[Fact]
	public void Required_Rule_FailsWhenEmpty()
	{
		var form = FormXml.FromXml("<form><text name='n' label='N:' required='true'/></form>");
		Assert.False(form.Validate());
		((PromptControl)form.GetEditor("n")).Input = "x";
		Assert.True(form.Validate());
	}

	[Fact]
	public void TypeInt_Rule_RejectsNonInteger()
	{
		var form = FormXml.FromXml("<form><text name='p' label='P:' type='int'/></form>");
		((PromptControl)form.GetEditor("p")).Input = "abc";
		Assert.False(form.Validate());
		((PromptControl)form.GetEditor("p")).Input = "42";
		Assert.True(form.Validate());
	}

	[Fact]
	public void Pattern_And_Message_Override()
	{
		var form = FormXml.FromXml(@"<form><text name='z' label='Zip:' pattern='^\d{5}$' message='5 digits'/></form>");
		((PromptControl)form.GetEditor("z")).Input = "abc";
		Assert.False(form.Validate());
		Assert.Equal("5 digits", form.ErrorTextForTest("z"));
	}

	[Fact]
	public void MinMaxLength_Enforced()
	{
		var form = FormXml.FromXml("<form><text name='u' label='U:' minLength='3'/></form>");
		((PromptControl)form.GetEditor("u")).Input = "ab";
		Assert.False(form.Validate());
		((PromptControl)form.GetEditor("u")).Input = "abc";
		Assert.True(form.Validate());
	}

	[Fact]
	public void NamedRule_Resolves_AndThrowsWhenMissing()
	{
		var reg = new Dictionary<string, System.Func<string?, string?>>
		{
			["nonEmpty"] = v => string.IsNullOrEmpty(v) ? "need a value" : null
		};
		var form = FormXml.FromXml("<form><text name='x' label='X:' rule='nonEmpty'/></form>", reg);
		Assert.False(form.Validate());  // empty → the named rule fires

		Assert.Throws<FormXmlException>(() =>
			FormXml.FromXml("<form><text name='x' label='X:' rule='missing'/></form>"));  // no registry entry
	}

	[Fact]
	public void Dropdown_Radio_Slider_Multiline_Build()
	{
		var form = FormXml.FromXml(@"
			<form>
			  <dropdown name='env' label='Env:' options='dev,staging,prod' initial='dev'/>
			  <radio name='mode' label='Mode:' options='Dev,Prod' initial='Dev'/>
			  <slider name='t' label='T:' min='0' max='60' initial='30'/>
			  <multiline name='notes' label='Notes:' initial='hi' height='4'/>
			</form>");
		var v = form.GetValues();
		Assert.Equal("dev", v["env"]);
		Assert.Equal("Dev", v["mode"]);
		Assert.Equal("30", v["t"]);
		Assert.Equal("hi", v["notes"]);
	}

	[Fact]
	public void Section_Collapsed_HidesFields_Row_PacksFields_Buttons_Submit()
	{
		var form = FormXml.FromXml(@"
			<form>
			  <section title='Advanced' collapsible='true' collapsed='true'>
			    <text name='dsn' label='DSN:'/>
			  </section>
			  <row>
			    <text name='first' label='First:'/>
			    <text name='last'  label='Last:'/>
			  </row>
			  <buttons ok='OK' cancel='Cancel'/>
			</form>");
		Assert.False(form.GetEditor("dsn") is IWindowControl w && w.Visible);  // collapsed → hidden
		Assert.Equal(1, form.RowGroupCountForTest());                          // first+last on one row
		bool submitted = false; form.Submitted += (_, __) => submitted = true;
		form.ClickOkForTest();
		Assert.True(submitted);
	}

	[Fact]
	public void MinMax_DecimalSchemaValue_ParsesUnderCommaDecimalCulture()
	{
		var prev = System.Globalization.CultureInfo.CurrentCulture;
		try
		{
			System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
			var form = FormXml.FromXml("<form><text name='w' label='W:' min='1.5' max='9.5'/></form>");
			((PromptControl)form.GetEditor("w")).Input = "0.5";   // below min
			Assert.False(form.Validate());
			((PromptControl)form.GetEditor("w")).Input = "5";     // in range
			Assert.True(form.Validate());
		}
		finally { System.Globalization.CultureInfo.CurrentCulture = prev; }
	}

	[Fact]
	public void Pattern_Invalid_ThrowsFormXmlException()
	{
		Assert.Throws<FormXmlException>(() =>
			FormXml.FromXml("<form><text name='x' label='X:' pattern='[unclosed'/></form>"));
	}

	[Fact]
	public void FromXmlFile_ReadsAndBuilds()
	{
		var path = System.IO.Path.GetTempFileName();
		System.IO.File.WriteAllText(path, "<form><text name='a' label='A:' initial='v'/></form>");
		try
		{
			var form = FormXml.FromXmlFile(path);
			Assert.Equal("v", form.GetValues()["a"]);
		}
		finally { System.IO.File.Delete(path); }
	}
}
