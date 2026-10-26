// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.DiagnosticsInternals;

/// <summary>
/// Every control reports what it currently is, for diagnostics.
/// </summary>
/// <remarks>
/// The layout tree already says where a control sits. <c>DescribeState</c> says what it
/// IS - checked, open, selected - which is the thing a probe could not previously see
/// and had to infer from pixels.
/// </remarks>
public class DescribeStateTests
{
	/// <summary>Collects described values so a test can assert on them.</summary>
	private sealed class Recorder : IStateWriter
	{
		public Dictionary<string, object?> Values { get; } = new();

		public void Write(string name, string? value) => Values[name] = value;
		public void Write(string name, long value) => Values[name] = value;
		public void Write(string name, double value) => Values[name] = value;
		public void Write(string name, bool value) => Values[name] = value;

		public void WriteIf(bool condition, string name, string? value)
		{
			if (condition) Values[name] = value;
		}
	}

	private static Dictionary<string, object?> Describe(BaseControl control)
	{
		var r = new Recorder();
		control.DescribeState(r);
		return r.Values;
	}

	[Fact]
	public void EveryControlDescribesSomething()
	{
		// The base contract: a control with no overridden state still reports
		// whether it can be interacted with, which is the first thing to check
		// when input appears to go nowhere.
		var rule = new RuleControl();

		Assert.NotEmpty(Describe(rule));
	}

	[Fact]
	public void APassiveControlSaysSo()
	{
		// A rule is decoration: no input, no focus, nothing to enable. Saying so
		// is more useful than reporting "enabled: true" for something that can
		// never receive anything.
		var rule = new RuleControl();

		Assert.Equal(false, Describe(rule)["interactive"]);
	}

	[Fact]
	public void MarkupIsInteractive_BecauseItSelectsAndLinks()
	{
		// Worth pinning: markup looks passive and is not - it supports selection,
		// copy and links, so it reports enabled rather than interactive: false.
		var markup = new MarkupControl(new List<string> { "text" });

		var state = Describe(markup);

		Assert.Contains("enabled", state.Keys);
		Assert.DoesNotContain("interactive", state.Keys);
	}

	[Fact]
	public void AnInteractiveControlReportsWhetherItIsEnabled()
	{
		var button = new ButtonControl { Text = "Go", IsEnabled = false };

		var state = Describe(button);

		Assert.Equal(false, state["enabled"]);
		Assert.DoesNotContain("interactive", state.Keys);
	}

	[Fact]
	public void ACheckboxReportsItsCheckedState()
	{
		var box = new CheckboxControl { Label = "Agree", Checked = true };

		var state = Describe(box);

		Assert.Equal(true, state["checked"]);

		// "text", not "label": a button's caption, a checkbox's label and a
		// prompt's input are the same question to a reader - what does it say? -
		// and a tool can only recognise the concept if they share a name.
		Assert.Equal("Agree", state["text"]);
	}

	[Fact]
	public void AButtonReportsItsText()
	{
		var button = new ButtonControl { Text = "Launch" };

		Assert.Equal("Launch", Describe(button)["text"]);
	}

	[Fact]
	public void ASliderReportsItsValueAndRange()
	{
		var slider = new SliderControl { MinValue = 0, MaxValue = 10, Value = 4 };

		var state = Describe(slider);

		Assert.Equal(4.0, state["value"]);
		Assert.Equal(0.0, state["min"]);
		Assert.Equal(10.0, state["max"]);
	}

	[Fact]
	public void AnOverrideKeepsTheBaseValues()
	{
		// base.DescribeState(w) first, then your own: a reader should never lose
		// the universal fields because a control added its own.
		var box = new CheckboxControl { Label = "x" };

		var state = Describe(box);

		Assert.Contains("enabled", state.Keys);   // from the base
		Assert.Contains("checked", state.Keys);   // from the override
	}

	[Fact]
	public void EveryControlInTheLibraryAnswers()
	{
		// DescribeState is virtual on BaseControl, so every control inherits a
		// default and this cannot fail by someone ADDING a control - which is the
		// point. It fails if a control is somehow made undescribable, and it fails
		// if the query itself stops finding anything.
		var controls = Controls();

		// Not a count. Asserting "at least N controls" would be a number to bump
		// every time one is added, and it would still pass if the query broke in a
		// way that happened to leave N behind. Naming a few controls that must be
		// in any such list says the same thing and never needs maintenance.
		foreach (var expected in new[]
		{
			typeof(ButtonControl), typeof(CheckboxControl), typeof(MarkupControl),
			typeof(ListControl), typeof(TableControl),
		})
		{
			Assert.Contains(expected, controls);
		}

		foreach (var t in controls)
		{
			var m = t.GetMethod(nameof(BaseControl.DescribeState),
				new[] { typeof(IStateWriter) });
			Assert.True(m != null, $"{t.Name} cannot describe itself");
		}
	}

	[Fact]
	public void EveryControlCanBeDescribedWithoutThrowing()
	{
		// The stronger check, and the one that catches a real mistake: an override
		// that dereferences something not yet set. A control is only diagnosable if
		// describing it is safe at any point in its life, including straight after
		// construction.
		var failures = new List<string>();

		foreach (var t in Controls())
		{
			if (t.GetConstructor(System.Type.EmptyTypes) == null) continue;

			BaseControl control;
			try
			{
				control = (BaseControl)System.Activator.CreateInstance(t)!;
			}
			catch
			{
				continue;   // needs more than a parameterless ctor; not this test's business
			}

			try
			{
				control.DescribeState(new Recorder());
			}
			catch (System.Exception ex)
			{
				failures.Add($"{t.Name}: {ex.GetType().Name}");
			}
			finally
			{
				try { control.Dispose(); } catch { /* disposal is not under test */ }
			}
		}

		Assert.True(failures.Count == 0, "these threw while describing themselves: "
			+ string.Join(", ", failures));
	}

	/// <summary>Every concrete control in the library.</summary>
	private static List<System.Type> Controls() =>
		typeof(BaseControl).Assembly.GetTypes()
			.Where(t => t.IsClass && !t.IsAbstract && typeof(BaseControl).IsAssignableFrom(t))
			.ToList();

	[Fact]
	public void AListReportsItsSelection()
	{
		var list = new ListControl();

		Assert.Contains("selectedIndex", Describe(list).Keys);
	}

	[Fact]
	public void AProgressBarReportsItsValue()
	{
		var bar = new ProgressBarControl { Value = 42 };

		Assert.Equal(42.0, Describe(bar)["value"]);
	}

	[Fact]
	public void AnEditorReportsItsLength_NotItsText()
	{
		// An editor can hold a megabyte; a probe asking what is on screen should
		// not be handed all of it.
		var edit = new MultilineEditControl { Content = "hello" };

		var state = Describe(edit);

		Assert.Equal(5L, state["length"]);
		Assert.DoesNotContain("content", state.Keys);
	}

	[Fact]
	public void DescribeStateRejectsANullWriter()
	{
		var button = new ButtonControl();

		Assert.Throws<System.ArgumentNullException>(() => button.DescribeState(null!));
	}
}
