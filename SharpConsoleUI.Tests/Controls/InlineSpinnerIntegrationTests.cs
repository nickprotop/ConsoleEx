// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System.Drawing;
using SharpConsoleUI;
using ControlsFactory = SharpConsoleUI.Builders.Controls;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

[Collection("InlineSpinner")]
public class InlineSpinnerIntegrationTests
{
	[Fact]
	public void MarkupControlAnimatesInlineSpinnerAcrossFrames()
	{
		MarkupSpinnerClock.SetTimeProviderForTests(() => 0); // frame 0
		try
		{
			var system = TestWindowSystemBuilder.CreateTestSystem();
			var window = new Window(system) { Width = 40, Height = 5 };
			var label = ControlsFactory.Markup("Loading [spinner circle]").Build();
			window.AddControl(label);

			var buf0 = new CharacterBuffer(40, 5);
			var bounds = new LayoutRect(0, 0, 40, 1);
			label.PaintDOM(buf0, bounds, bounds, Color.White, Color.Black);

			// Advance the clock by one interval -> next frame. The inline [spinner circle]
			// animates at Circle's per-style default interval, so advance by exactly that.
			int circleInterval = SpinnerControl.DefaultIntervalMs(SpinnerStyle.Circle);
			MarkupSpinnerClock.SetTimeProviderForTests(() => circleInterval);
			var buf1 = new CharacterBuffer(40, 5);
			label.PaintDOM(buf1, bounds, bounds, Color.White, Color.Black);

			// The spinner glyph follows "Loading " (8 chars). Find the column that changed.
			bool anyChanged = false;
			for (int x = 0; x < 40; x++)
			{
				if (!buf0.GetCell(x, 0).Character.Equals(buf1.GetCell(x, 0).Character))
				{
					anyChanged = true;
					break;
				}
			}
			Assert.True(anyChanged, "The inline spinner glyph should differ between two paints one interval apart.");
		}
		finally { MarkupSpinnerClock.ResetTimeProviderForTests(); MarkupSpinnerClock.ResetForTests(); }
	}
}
