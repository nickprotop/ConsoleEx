// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// A role's body text colour: what plain text is drawn in, and what it leaves alone.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A role can dim its whole message -- system notes drawn quieter than the
	/// conversation -- without touching what the content itself coloured. The
	/// split is the point: markdown writes an explicit colour for a heading, a
	/// link, a quote and a table's borders, and writes none for plain text, so
	/// plain text is exactly what inherits the control's default foreground.
	/// </para>
	/// <para>
	/// Unset has to render as it always did, since every existing role leaves it
	/// unset; and clearing it again has to RESET the colour rather than leave the
	/// last one painted, which is the same "unset versus explicit" care the
	/// background already takes on a restyle.
	/// </para>
	/// </remarks>
	public class ChatRoleForegroundTests
	{
		private static readonly Color Dim = new(120, 120, 120);

		private static (ConsoleWindowSystem system, ChatTranscriptControl chat) Host()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(60, 20);
			var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 20 };
			var chat = new ChatTranscriptControl { VerticalAlignment = VerticalAlignment.Fill };
			window.AddControl(chat);
			system.AddWindow(window);
			return (system, chat);
		}

		private static void Draw(ConsoleWindowSystem system)
		{
			system.Render.UpdateDisplay();
			system.Render.UpdateDisplay();
		}

		private static string RowText(SharpConsoleUI.Diagnostics.Snapshots.CharacterBufferSnapshot snap, int y, int width)
		{
			var sb = new System.Text.StringBuilder();
			for (int x = 0; x < width; x++) sb.Append((char)snap.GetCell(x, y).Character.Value);
			return sb.ToString();
		}

		/// <summary>The foreground of the first cell of <paramref name="needle"/>, wherever it is painted.</summary>
		private static Color? ForegroundOf(ConsoleWindowSystem system, string needle)
		{
			var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
			for (int y = 0; y < snap.Height; y++)
			{
				string row = RowText(snap, y, snap.Width);
				int at = row.IndexOf(needle, System.StringComparison.Ordinal);
				if (at >= 0) return snap.GetCell(at, y).Foreground;
			}
			return null;
		}

		[Fact]
		public void PlainMarkdownText_TakesTheRolesForeground()
		{
			var (system, chat) = Host();
			chat.SetRoleStyle(ChatRole.System, new ChatRoleStyle { Foreground = Dim });

			chat.AddMessage(ChatRole.System, "plainbody");
			Draw(system);

			Assert.Equal(Dim, ForegroundOf(system, "plainbody"));
		}

		[Fact]
		public void ContentThatColoursItself_KeepsItsOwnColour()
		{
			var (system, chat) = Host();
			chat.SetRoleStyle(ChatRole.System, new ChatRoleStyle { Foreground = Dim });

			// A heading, inline code and a table all write an explicit colour of
			// their own; only the paragraph is left to inherit.
			chat.AddMessage(ChatRole.System,
				"# Headingword\n\nplainbody with `codeword` inside\n\n| Colone |\n| --- |\n| Cellone |");
			Draw(system);

			Assert.Equal(Dim, ForegroundOf(system, "plainbody"));

			var heading = ForegroundOf(system, "Headingword");
			var code = ForegroundOf(system, "codeword");
			Assert.NotNull(heading);
			Assert.NotNull(code);
			Assert.NotEqual(Dim, heading!.Value);
			Assert.NotEqual(Dim, code!.Value);
		}

		[Fact]
		public void Restyle_RecoloursAnExistingMessage_AndNullRestoresTheDefault()
		{
			var (system, chat) = Host();

			// The role keeps one style throughout; only Foreground is set and
			// cleared, so the baseline this compares against is the role's own
			// colour and not some other style's.
			static ChatRoleStyle Base(Color? fg) => new() { ShowHeader = false, Foreground = fg };
			chat.SetRoleStyle(ChatRole.System, Base(null));
			chat.AddMessage(ChatRole.System, "plainbody");
			Draw(system);

			var before = ForegroundOf(system, "plainbody");
			Assert.NotNull(before);
			Assert.NotEqual(Dim, before!.Value);

			// Set it, then RestyleMessages -- the documented pair, since SetRoleStyle
			// deliberately leaves messages already on screen alone. This is exactly
			// what a host does on a theme switch.
			chat.SetRoleStyle(ChatRole.System, Base(Dim));
			chat.RestyleMessages();
			Draw(system);
			Assert.Equal(Dim, ForegroundOf(system, "plainbody"));

			// Clear it: the colour must go back, not stay on the last value set.
			chat.SetRoleStyle(ChatRole.System, Base(null));
			chat.RestyleMessages();
			Draw(system);
			Assert.Equal(before.Value, ForegroundOf(system, "plainbody"));
		}

		[Fact]
		public void RoleWithoutForeground_RendersAsBefore()
		{
			// The guard on the unset path: every role that exists today leaves
			// Foreground null, and those must be untouched by this feature.
			var (withSystem, withChat) = Host();
			withChat.SetRoleStyle(ChatRole.System, new ChatRoleStyle { ShowHeader = false });
			withChat.AddMessage(ChatRole.System, "plainbody");
			Draw(withSystem);
			var unset = ForegroundOf(withSystem, "plainbody");

			var (plainSystem, plainChat) = Host();
			plainChat.SetRoleStyle(ChatRole.System, new ChatRoleStyle { ShowHeader = false, Foreground = null });
			plainChat.AddMessage(ChatRole.System, "plainbody");
			Draw(plainSystem);

			Assert.Equal(unset, ForegroundOf(plainSystem, "plainbody"));
			System.GC.KeepAlive(withSystem);
			System.GC.KeepAlive(plainSystem);
		}
	}
}
