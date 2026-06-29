// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Layout;
using Xunit;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Flows
{
	public class FlowAutoSizeHeightTests
	{
		private static IWindowControl Body(int lines)
		{
			var p = Ctl.ScrollablePanel().WithVerticalAlignment(VerticalAlignment.Fill).Build();
			for (int i = 0; i < lines; i++)
				p.AddControl(Ctl.Markup().AddLine($"line {i}").Build());
			return p;
		}

		[Fact]
		public void ExplicitHeightHint_Wins_EvenWithAutoSize()
		{
			var chrome = new FlowChrome("T", heightHint: 20, autoSizeHeight: true);
			int h = FlowContentHelpers.ResolveWindowHeight(chrome, Body(3), windowWidth: 50,
				bandRows: 6, terminalHeight: 50, fixedDefault: 13);
			Assert.Equal(20, h);
		}

		[Fact]
		public void NoAutoSize_NullHint_UsesFixedDefault()
		{
			var chrome = new FlowChrome("T"); // autoSize false, hint null
			int h = FlowContentHelpers.ResolveWindowHeight(chrome, Body(3), windowWidth: 50,
				bandRows: 6, terminalHeight: 50, fixedDefault: 13);
			Assert.Equal(13, h);
		}

		[Fact]
		public void AutoSize_ShortBody_ClampsToMin()
		{
			// 1 body row + 6 band rows = 7 natural; min is 7 → expect at least the min, not below.
			var chrome = new FlowChrome("T", autoSizeHeight: true);
			int h = FlowContentHelpers.ResolveWindowHeight(chrome, Body(1), windowWidth: 50,
				bandRows: 6, terminalHeight: 50, fixedDefault: 13);
			Assert.True(h >= ControlDefaults.FlowAutoSizeMinHeight, $"got {h}");
			Assert.True(h < 13, $"short auto-sized window should be tighter than the fixed default, got {h}");
		}

		[Fact]
		public void AutoSize_TallBody_CapsAtTerminalMinusMargin()
		{
			// 100 body rows would blow past a 20-row terminal → cap = 20 - margin.
			var chrome = new FlowChrome("T", autoSizeHeight: true);
			int terminal = 20;
			int h = FlowContentHelpers.ResolveWindowHeight(chrome, Body(100), windowWidth: 50,
				bandRows: 6, terminalHeight: terminal, fixedDefault: 13);
			Assert.Equal(terminal - ControlDefaults.FlowAutoSizeCapMargin, h);
		}

		[Fact]
		public void AutoSize_MidBody_FitsContentPlusBands()
		{
			// 5 body rows + 6 band rows = 11 natural, within [min, cap] → exactly 11.
			var chrome = new FlowChrome("T", autoSizeHeight: true);
			int h = FlowContentHelpers.ResolveWindowHeight(chrome, Body(5), windowWidth: 50,
				bandRows: 6, terminalHeight: 50, fixedDefault: 13);
			Assert.Equal(11, h);
		}
	}
}
