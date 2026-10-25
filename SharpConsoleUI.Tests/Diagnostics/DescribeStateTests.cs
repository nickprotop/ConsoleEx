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
		Assert.Equal("Agree", state["label"]);
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
	public void DescribeStateRejectsANullWriter()
	{
		var button = new ButtonControl();

		Assert.Throws<System.ArgumentNullException>(() => button.DescribeState(null!));
	}
}
