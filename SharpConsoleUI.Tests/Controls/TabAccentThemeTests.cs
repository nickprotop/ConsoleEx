// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// The accented separator under the active tab, and the rule beside it.
	/// </summary>
	/// <remarks>
	/// Both were hardcoded Cyan1 and Grey, so a tab strip drew a bright cyan
	/// underline on every theme - the one piece of the control that ignored the
	/// palette while its own headers followed it correctly. On a dark blue-grey
	/// theme the cyan read as a stray highlight from some other application.
	/// </remarks>
	public class TabAccentThemeTests
	{
		private static (ConsoleWindowSystem System, TabControl Tabs) Build(
			string theme, int width = 60, int height = 14)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
			system.ThemeStateService.SwitchTheme(theme);

			var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };

			var tabs = new TabControl
			{
				HeaderStyle = TabHeaderStyle.AccentedSeparator,
				VerticalAlignment = VerticalAlignment.Fill,
			};
			tabs.AddTab("one", new MarkupControl(new System.Collections.Generic.List<string> { "first" }));
			tabs.AddTab("two", new MarkupControl(new System.Collections.Generic.List<string> { "second" }));

			window.AddControl(tabs);
			system.AddWindow(window);
			return (system, tabs);
		}

		private static CharacterBufferSnapshot Draw(ConsoleWindowSystem system)
		{
			system.Render.UpdateDisplay();
			system.Render.UpdateDisplay();
			return system.RenderingDiagnostics!.LastBufferSnapshot!;
		}

		/// <summary>True when any cell carries the colour as its foreground.</summary>
		private static bool AnyForeground(CharacterBufferSnapshot snap, Color c)
		{
			for (int y = 0; y < snap.Height; y++)
				for (int x = 0; x < snap.Width; x++)
					if (snap.GetCell(x, y).Foreground == c) return true;
			return false;
		}

		[Theory]
		[InlineData("Slate")]
		[InlineData("Daylight")]
		public void TheAccentedSeparatorIsNotAHardcodedCyan(string theme)
		{
			// Cyan1 was the literal the renderer drew whatever the palette said.
			// Checked on themes that do NOT use it: ModernGray is excluded because
			// Cyan1 is genuinely its PrimaryColor, so cyan there is the theme
			// being followed rather than ignored - asserting its absence would
			// test the theme's taste instead of the renderer's behaviour.
			var (system, _) = Build(theme);

			Assert.False(AnyForeground(Draw(system), Color.Cyan1),
				$"{theme}: the tab strip drew hardcoded Cyan1");
			GC.KeepAlive(system);
		}



		[Fact]
		public void TheAccentFollowsTheThemeItIsDrawnUnder()
		{
			// Two themes with different accents must not produce the same strip.
			// Asserting "not cyan" alone would pass against a renderer that
			// hardcoded some other colour instead.
			var slate = Draw(Build("Slate").System);
			var daylight = Draw(Build("Daylight").System);

			bool differs = false;
			for (int y = 0; y < Math.Min(slate.Height, daylight.Height) && !differs; y++)
			{
				for (int x = 0; x < Math.Min(slate.Width, daylight.Width); x++)
				{
					if (slate.GetCell(x, y).Foreground != daylight.GetCell(x, y).Foreground)
					{
						differs = true;
						break;
					}
				}
			}

			Assert.True(differs, "the tab strip painted identically under two different themes");
		}

		[Theory]
		[InlineData("Slate")]
		[InlineData("ModernGray")]
		[InlineData("Daylight")]
		public void TheAccentIsVisibleAgainstTheStrip(string theme)
		{
			// The property both wrong answers broke. Hardcoded Cyan1 was WRONG for
			// the theme but at least visible; taking the active tab's background
			// was right for the theme and invisible, because an active tab sits on
			// the window background. A mark nobody can see marks nothing - this
			// pins the one thing that has to hold however the colour is derived.
			var (system, _) = Build(theme);
			var snap = Draw(system);

			Color bg = snap.GetCell(0, 1).Background;

			double best = 0;
			for (int x = 0; x < snap.Width; x++)
			{
				var cell = snap.GetCell(x, 1);
				int ch = cell.Character.Value;
				if (ch != '\u2550' && ch != '\u2561' && ch != '\u255e') continue;

				double r = SharpConsoleUI.Helpers.PaletteColors.ContrastRatio(cell.Foreground, bg);
				if (r > best) best = r;
			}

			Assert.True(best >= SharpConsoleUI.Helpers.PaletteColors.GraphicContrast,
				$"{theme}: the accent reads {best:0.00}:1 against the strip, "
					+ $"below the {SharpConsoleUI.Helpers.PaletteColors.GraphicContrast}:1 floor");
			GC.KeepAlive(system);
		}



	}
}
