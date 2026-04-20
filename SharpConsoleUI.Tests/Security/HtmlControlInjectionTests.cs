// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Html;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Security
{
	/// <summary>
	/// Proves that HtmlInlineFlow/HtmlBlockFlow construct Cell objects from
	/// untrusted HTML text without sanitization. These tests SHOULD FAIL until
	/// the vulnerability is fixed — they demonstrate the exploit.
	/// </summary>
	public class HtmlControlInjectionTests
	{
		[Fact]
		public void HtmlLayout_EscapeInTextNode_ReachesOutputCells()
		{
			// HTML with ESC (U+001B) embedded in text content
			string html = "<p>hello\x1b[31mred</p>";

			var engine = new HtmlLayoutEngine();
			var result = engine.Layout(html, 40, Color.White, Color.Black);

			// Check if any cell contains ESC
			bool foundEsc = false;
			foreach (var line in result.Lines)
			{
				foreach (var cell in line.Cells)
				{
					if (cell.Character == new System.Text.Rune('\x1b'))
					{
						foundEsc = true;
						break;
					}
				}
				if (foundEsc) break;
			}

			// This SHOULD be false (safe), but currently IS true (vulnerable)
			Assert.False(foundEsc,
				"ESC (U+001B) from HTML text reached output cells unsanitized. " +
				"HtmlInlineFlow/HtmlBlockFlow must filter through TextSanitizer.");
		}

		[Fact]
		public void HtmlLayout_BiDiOverrideInText_ReachesOutputCells()
		{
			// HTML with RLO (U+202E) — Trojan Source attack
			string html = "<p>admin\u202eresu</p>";

			var engine = new HtmlLayoutEngine();
			var result = engine.Layout(html, 40, Color.White, Color.Black);

			bool foundBiDi = false;
			foreach (var line in result.Lines)
			{
				foreach (var cell in line.Cells)
				{
					int cp = cell.Character.Value;
					if (cp >= 0x202A && cp <= 0x202E)
					{
						foundBiDi = true;
						break;
					}
				}
				if (foundBiDi) break;
			}

			Assert.False(foundBiDi,
				"BiDi override U+202E from HTML text reached output cells. " +
				"Trojan Source attack is possible via email/web content.");
		}

		[Fact]
		public void HtmlLayout_C1ControlInText_ReachesOutputCells()
		{
			// HTML with C1 CSI (U+009B) — equivalent to ESC[
			string html = "<p>text\u009B31minjected</p>";

			var engine = new HtmlLayoutEngine();
			var result = engine.Layout(html, 40, Color.White, Color.Black);

			bool foundC1 = false;
			foreach (var line in result.Lines)
			{
				foreach (var cell in line.Cells)
				{
					int cp = cell.Character.Value;
					if (cp >= 0x0080 && cp <= 0x009F)
					{
						foundC1 = true;
						break;
					}
				}
				if (foundC1) break;
			}

			Assert.False(foundC1,
				"C1 control character from HTML text reached output cells. " +
				"Terminal escape injection via 8-bit controls is possible.");
		}

		[Fact]
		public void HtmlLayout_LegitText_PreservedCorrectly()
		{
			// Normal HTML without control characters should render fine
			string html = "<p>Hello, World!</p>";

			var engine = new HtmlLayoutEngine();
			var result = engine.Layout(html, 40, Color.White, Color.Black);

			Assert.True(result.Lines.Length > 0);
			bool foundH = false;
			foreach (var line in result.Lines)
			{
				foreach (var cell in line.Cells)
				{
					if (cell.Character == new System.Text.Rune('H'))
					{
						foundH = true;
						break;
					}
				}
			}
			Assert.True(foundH, "Normal text 'H' should be present in output");
		}
	}
}
