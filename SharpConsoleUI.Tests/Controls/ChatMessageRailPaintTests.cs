// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	public class ChatMessageRailPaintTests
	{
		private static (ConsoleWindowSystem system, ChatTranscriptControl chat) Host()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(40, 16);
			var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 16 };
			var chat = new ChatTranscriptControl { VerticalAlignment = VerticalAlignment.Fill };
			window.AddControl(chat);
			system.AddWindow(window);
			return (system, chat);
		}

		[Fact]
		public void RailedMessage_PaintsRailGlyph_OnBodyRow_NotHeaderRow()
		{
			var (system, chat) = Host();
			var id = chat.AddMessage(ChatRole.Assistant, "line one\nline two");
			chat.SetStatus(id, "done");
			system.Render.UpdateDisplay(); system.Render.UpdateDisplay();

			var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
			// Find the header row (contains the role label "Assistant") and a body row (contains "line one").
			int headerRow = -1, bodyRow = -1;
			for (int y = 0; y < snap.Height; y++)
			{
				string row = RowText(snap, y, snap.Width);
				if (headerRow < 0 && row.Contains("Assistant")) headerRow = y;
				if (bodyRow < 0 && row.Contains("line one")) bodyRow = y;
			}
			Assert.True(bodyRow >= 0, "body row not found");
			Assert.True(headerRow >= 0, "header row not found");
			// The rail '│' appears at column 0 on the body row, and NOT on the header row.
			Assert.Equal("│", snap.GetCell(0, bodyRow).Character.ToString());
			Assert.NotEqual("│", snap.GetCell(0, headerRow).Character.ToString());
			System.GC.KeepAlive(system);
		}

		[Fact]
		public void Rail_StopsAtStatusRow_DoesNotLeakOntoFooterSpacerLine()
		{
			// The footer's bottommost row carries a 1-line bottom-margin spacer (blank line before the next
			// message). The rail must stop at the last content (status) row and NOT paint the empty spacer
			// row below it, or it would leak a '│' into the gap between messages.
			var (system, chat) = Host();
			var id = chat.AddMessage(ChatRole.Assistant, "line one");
			chat.SetStatus(id, "done");
			// A following message so the spacer row is a real blank gap between the two.
			chat.AddMessage(ChatRole.Assistant, "second message");
			system.Render.UpdateDisplay(); system.Render.UpdateDisplay();

			var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
			int statusRow = -1;
			for (int y = 0; y < snap.Height; y++)
				if (RowText(snap, y, snap.Width).Contains("done")) { statusRow = y; break; }
			Assert.True(statusRow >= 0, "status row not found");
			// Status row IS railed; the row immediately below it (the spacer gap) is NOT.
			Assert.Equal("│", snap.GetCell(0, statusRow).Character.ToString());
			Assert.NotEqual("│", snap.GetCell(0, statusRow + 1).Character.ToString());
			System.GC.KeepAlive(system);
		}

		[Fact]
		public void NoFooterMessage_PaintsNoRail()
		{
			var (system, chat) = Host();
			// Assistant is a borderless role, so a plain (no-footer) message draws no border and no rail —
			// column 0 stays glyph-free. (A bordered role like User would draw its own '│' border edge,
			// which is not the rail; the borderless role isolates the rail's presence/absence.)
			var id = chat.AddMessage(ChatRole.Assistant, "hi"); // no footer
			System.GC.KeepAlive(id);
			system.Render.UpdateDisplay(); system.Render.UpdateDisplay();
			var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
			for (int y = 0; y < snap.Height; y++)
				Assert.NotEqual("│", snap.GetCell(0, y).Character.ToString());
			System.GC.KeepAlive(system);
		}

		[Fact]
		public void PartiallyScrolled_RailStaysInsideContentViewport_NoChromeBleed()
		{
			// A short window with far more content than fits, scrolled so a railed message is partially
			// off the top. The rail must paint only inside the scrollbar-reduced content viewport — never
			// on the vertical-scrollbar column and never on a row outside [0, ContentViewportHeight).
			var system = TestWindowSystemBuilder.CreateTestSystem(40, 12);
			var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
			var chat = new ChatTranscriptControl { VerticalAlignment = VerticalAlignment.Fill };
			window.AddControl(chat);
			system.AddWindow(window);
			for (int i = 0; i < 6; i++)
			{
				var id = chat.AddMessage(ChatRole.Assistant, $"msg{i} line one\nmsg{i} line two\nmsg{i} line three");
				chat.SetStatus(id, $"done{i}");
			}
			chat.AutoScroll = false;
			chat.ScrollVerticalBy(4); // partially scroll a message off the top
			system.Render.UpdateDisplay(); system.Render.UpdateDisplay();

			var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;

			// Precondition: the transcript overflows (there is somewhere to scroll and a scrollbar shows).
			Assert.True(chat.TotalContentHeight > chat.ViewportHeight, "precondition: transcript must overflow.");

			int contentVpH = chat.ContentViewportHeight;

			// The rail lives only in the gutter (column 0). It must never paint on a row at or below the
			// content-viewport height (that would be chrome / off-viewport bleed). (The scrollbar's OWN
			// vertical track also uses '│' in its right-edge column — that is base chrome, not the rail, so
			// we only assert about the gutter column here.)
			for (int y = contentVpH; y < snap.Height; y++)
				Assert.NotEqual("│", snap.GetCell(0, y).Character.ToString());

			// A rail IS actually painted somewhere inside the viewport gutter (the test is meaningful), and
			// a partially-scrolled message whose header is above the fold still rails its visible body rows.
			bool railSeen = false;
			for (int y = 0; y < contentVpH && !railSeen; y++)
				railSeen = snap.GetCell(0, y).Character.ToString() == "│";
			Assert.True(railSeen, "expected at least one rail glyph inside the content viewport.");

			// The header row of a fully-visible message inside the viewport carries no rail (stays flush).
			int headerRow = -1;
			for (int y = 0; y < contentVpH; y++)
			{
				string row = RowText(snap, y, snap.Width);
				if (row.Contains("Assistant")) { headerRow = y; break; }
			}
			Assert.True(headerRow >= 0, "a message header must be visible in the viewport.");
			Assert.NotEqual("│", snap.GetCell(0, headerRow).Character.ToString());
			System.GC.KeepAlive(system);
		}

		[Fact]
		public void FooterToolbar_Wraps_AndRailCoversTheWrappedRows()
		{
			// The footer actions toolbar wraps overflow onto extra rows (rather than clipping). The rail's
			// height-based span must cover the wrapped toolbar's full height — every actions row is railed.
			var system = TestWindowSystemBuilder.CreateTestSystem(24, 16); // narrow → forces wrap
			var window = new Window(system) { Left = 0, Top = 0, Width = 24, Height = 16 };
			var chat = new ChatTranscriptControl { VerticalAlignment = VerticalAlignment.Fill };
			window.AddControl(chat);
			system.AddWindow(window);

			var id = chat.AddMessage(ChatRole.Assistant, "body");
			chat.SetActions(id, new[]
			{
				new ChatMessageAction { Id = "a", Label = "Alpha" },
				new ChatMessageAction { Id = "b", Label = "Bravo" },
				new ChatMessageAction { Id = "c", Label = "Charlie" },
				new ChatMessageAction { Id = "d", Label = "Delta" },
			});
			system.Render.UpdateDisplay(); system.Render.UpdateDisplay();

			var toolbar = chat.ActionsToolbarForTest(id);
			Assert.NotNull(toolbar);
			Assert.True(toolbar!.Wrap, "the footer actions toolbar must be set to wrap so overflow flows to a 2nd row, not clipped.");

			// The toolbar occupies more than one row (it wrapped); assert the rail is painted on the toolbar's
			// row AND the row below it (the wrapped 2nd actions row), i.e. the rail covers the taller footer.
			var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
			int firstActionRow = -1;
			for (int y = 0; y < snap.Height; y++)
				if (RowText(snap, y, snap.Width).Contains("Alpha")) { firstActionRow = y; break; }
			Assert.True(firstActionRow >= 0, "wrapped actions row not found");
			Assert.Equal("│", snap.GetCell(0, firstActionRow).Character.ToString());
			Assert.Equal("│", snap.GetCell(0, firstActionRow + 1).Character.ToString()); // the wrapped 2nd row is railed too
			System.GC.KeepAlive(system);
		}

		private static string RowText(SharpConsoleUI.Diagnostics.Snapshots.CharacterBufferSnapshot snap, int y, int w)
		{
			var sb = new System.Text.StringBuilder();
			for (int x = 0; x < w; x++) sb.Append(snap.GetCell(x, y).Character.ToString());
			return sb.ToString();
		}
	}
}
