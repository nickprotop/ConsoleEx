// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using ControlsFactory = SharpConsoleUI.Builders.Controls;

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

	/// <summary>
	/// "Real thing" test: an inline [spinner] must animate through the ACTUAL render loop — a window
	/// whose only animated content is inline markup, repainted by Render.UpdateDisplay() with no input
	/// and no SpinnerControl on screen to dirty the window as a side effect. The isolated test above
	/// passes even when this fails, because it calls PaintDOM directly and so bypasses the dirty gate
	/// (RenderWindows skips any window with PendingWork == FrameWork.None).
	/// </summary>
	[Fact]
	public void InlineSpinnerAnimatesThroughRenderLoopWithoutInput()
	{
		int interval = SpinnerControl.DefaultIntervalMs(SpinnerStyle.Circle);
		long now = 0;
		MarkupSpinnerClock.SetTimeProviderForTests(() => now);
		try
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(60, 12);
			var window = new Window(system) { Width = 40, Height = 5, Left = 1, Top = 1 };
			window.AddControl(ControlsFactory.Markup("Loading [spinner circle] please wait").Build());
			system.AddWindow(window);

			// Frame A: first real paint through the render loop.
			MarkupSpinnerClock.Tick(system.Animations.IsEnabled);
			system.Render.UpdateDisplay();
			string rowA = ReadContentRow(window);

			// Advance the spinner clock one full interval. No input, no other animation.
			now += interval;

			// Frame B: exactly what the main loop does on the next tick while a spinner is on screen.
			MarkupSpinnerClock.Tick(system.Animations.IsEnabled);
			system.Render.UpdateDisplay();
			string rowB = ReadContentRow(window);

			Assert.NotEqual(rowA, rowB);
		}
		finally { MarkupSpinnerClock.ResetTimeProviderForTests(); MarkupSpinnerClock.ResetForTests(); }
	}

	/// <summary>
	/// Pins the mechanism the fix relies on: after a paint, a Tick that crosses a frame boundary must
	/// leave the window DIRTY. RenderWindows skips a window whose PendingWork is None, so without this
	/// the main loop's per-tick UpdateDisplay repaints nothing and the glyph freezes until some
	/// unrelated event (a key, a click, a neighbouring SpinnerControl) dirties the window instead.
	/// </summary>
	[Fact]
	public void TickDirtiesWindowHostingInlineSpinner()
	{
		int interval = SpinnerControl.DefaultIntervalMs(SpinnerStyle.Circle);
		long now = 0;
		MarkupSpinnerClock.SetTimeProviderForTests(() => now);
		try
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(60, 12);
			var window = new Window(system) { Width = 40, Height = 5, Left = 1, Top = 1 };
			window.AddControl(ControlsFactory.Markup("Loading [spinner circle]").Build());
			system.AddWindow(window);

			// Paint once, then let the render settle so the window reports no pending work.
			system.Render.UpdateDisplay();
			Assert.Equal(FrameWork.None, window.PendingWork);

			now += interval;
			MarkupSpinnerClock.Tick(system.Animations.IsEnabled);

			Assert.NotEqual(FrameWork.None, window.PendingWork);
		}
		finally { MarkupSpinnerClock.ResetTimeProviderForTests(); MarkupSpinnerClock.ResetForTests(); }
	}

	/// <summary>Reads the first row of the window's rendered content buffer.</summary>
	/// <summary>
	/// A COLLAPSIBLE PANEL'S HEADER animates its inline spinner too.
	///
	/// <para>The registry fix covered <see cref="MarkupControl"/>, which is the only type that calls
	/// RegisterHost. A CollapsiblePanel parses its OWN header markup instead of hosting a
	/// MarkupControl, so it was never registered: the clock ticked on time and had nobody to
	/// invalidate, and the header repainted only when something ELSE dirtied the window.</para>
	///
	/// <para>That makes the symptom a cadence rather than a freeze, which is why it survived the
	/// original fix — a header spinner silently adopts whatever the app's busiest timer runs at.
	/// Measured in a chat transcript: ~960ms per frame, the application's one-second panel clock,
	/// against the 60ms the tag asked for.</para>
	/// </summary>
	[Fact]
	public void CollapsiblePanelHeaderAnimatesItsInlineSpinner()
	{
		int interval = SpinnerControl.DefaultIntervalMs(SpinnerStyle.Circle);
		long now = 0;
		MarkupSpinnerClock.SetTimeProviderForTests(() => now);
		try
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(60, 12);
			var window = new Window(system) { Width = 40, Height = 6, Left = 1, Top = 1 };

			// NOTHING ELSE ANIMATED on screen — no MarkupControl, no SpinnerControl. Any of those
			// would dirty the window on their own and drag the header along, which is exactly how
			// this went unnoticed.
			var panel = new CollapsiblePanel { Title = "Tool [spinner circle] running" };
			window.AddControl(panel);
			system.AddWindow(window);

			MarkupSpinnerClock.Tick(system.Animations.IsEnabled);
			system.Render.UpdateDisplay();
			string rowA = ReadContentRow(window);

			now += interval;

			MarkupSpinnerClock.Tick(system.Animations.IsEnabled);
			system.Render.UpdateDisplay();
			string rowB = ReadContentRow(window);

			Assert.NotEqual(rowA, rowB);
		}
		finally { MarkupSpinnerClock.ResetTimeProviderForTests(); MarkupSpinnerClock.ResetForTests(); }
	}

	private static string ReadContentRow(Window window)
	{
		var buffer = window.ContentBuffer;
		Assert.NotNull(buffer);
		var sb = new System.Text.StringBuilder();
		for (int x = 0; x < buffer!.Width; x++)
			sb.Append(buffer.GetCell(x, 0).Character);
		return sb.ToString();
	}
}
