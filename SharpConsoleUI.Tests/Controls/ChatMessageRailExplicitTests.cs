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
	/// <summary>
	/// A message's left rail can be turned on (or off) explicitly, independently of whether the message
	/// has footer chrome. Before this the rail was derived solely from <c>HasFooter</c>, so the only way
	/// to rail a message was to give it an actions row or a status row it did not otherwise need.
	/// </summary>
	public class ChatMessageRailExplicitTests
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

		private static string RowText(SharpConsoleUI.Diagnostics.Snapshots.CharacterBufferSnapshot snap, int y, int w)
		{
			var sb = new System.Text.StringBuilder();
			for (int x = 0; x < w; x++) sb.Append(snap.GetCell(x, y).Character.ToString());
			return sb.ToString();
		}

		private static int RowContaining(SharpConsoleUI.Diagnostics.Snapshots.CharacterBufferSnapshot snap, string needle)
		{
			for (int y = 0; y < snap.Height; y++)
				if (RowText(snap, y, snap.Width).Contains(needle)) return y;
			return -1;
		}

		/// <summary>
		/// The headline: a plain message with NO footer and NO status can be railed on request, and the
		/// rail is actually painted down its body.
		/// </summary>
		[Fact]
		public void SetMessageRail_True_RailsAFooterlessMessage()
		{
			var (system, chat) = Host();
			var id = chat.AddMessage(ChatRole.Assistant, "line one\nline two");
			chat.SetMessageRail(id, true);
			system.Render.UpdateDisplay(); system.Render.UpdateDisplay();

			var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
			int bodyRow = RowContaining(snap, "line one");
			Assert.True(bodyRow >= 0, "body row not found");
			Assert.Equal("│", snap.GetCell(0, bodyRow).Character.ToString());
			System.GC.KeepAlive(system);
		}

		/// <summary>
		/// The gutter inset and the painted glyph must agree. If only one of the two gates learned about
		/// the override, the rail would land in a column the body was never inset past (rail over text) or
		/// an empty gutter would open with no rail in it.
		/// </summary>
		[Fact]
		public void SetMessageRail_True_ReservesTheGutter_OnAFooterlessMessage()
		{
			var (_, chat) = Host();
			var id = chat.AddMessage(ChatRole.Assistant, "body");
			Assert.Equal(0, chat.BodyLeftMarginForTest(id));

			chat.SetMessageRail(id, true);
			Assert.Equal(chat.MessageRailGutterWidth, chat.BodyLeftMarginForTest(id));
		}

		/// <summary>
		/// The other direction: a message that WOULD be railed by the footer rule can opt out explicitly.
		/// </summary>
		[Fact]
		public void SetMessageRail_False_SuppressesTheRailOnAFooteredMessage()
		{
			var (system, chat) = Host();
			var id = chat.AddMessage(ChatRole.Assistant, "line one");
			chat.SetStatus(id, "done");
			Assert.Equal(chat.MessageRailGutterWidth, chat.BodyLeftMarginForTest(id));

			chat.SetMessageRail(id, false);
			Assert.Equal(0, chat.BodyLeftMarginForTest(id));

			system.Render.UpdateDisplay(); system.Render.UpdateDisplay();
			var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
			int bodyRow = RowContaining(snap, "line one");
			Assert.True(bodyRow >= 0, "body row not found");
			Assert.NotEqual("│", snap.GetCell(0, bodyRow).Character.ToString());
			System.GC.KeepAlive(system);
		}

		/// <summary>
		/// Passing null restores the inherited footer-gated behaviour, so an override can be undone
		/// without the caller tracking what the default would have been.
		/// </summary>
		[Fact]
		public void SetMessageRail_Null_RestoresFooterGatedDefault()
		{
			var (_, chat) = Host();
			var id = chat.AddMessage(ChatRole.Assistant, "body");
			chat.SetStatus(id, "done");

			chat.SetMessageRail(id, false);
			Assert.Equal(0, chat.BodyLeftMarginForTest(id));

			chat.SetMessageRail(id, null);
			Assert.Equal(chat.MessageRailGutterWidth, chat.BodyLeftMarginForTest(id));
		}

		/// <summary>
		/// An explicit rail must not drag the footer's bracketing with it. The panel's bottom margin is the
		/// gap BETWEEN messages; it is collapsed only when real footer/peek siblings follow the panel and
		/// need to sit contiguously. A railed-but-footerless message has no such siblings, so collapsing it
		/// would delete the gap before the next message.
		/// </summary>
		[Fact]
		public void SetMessageRail_True_DoesNotCollapseTheBetweenMessageGap()
		{
			var (_, chat) = Host();
			var id = chat.AddMessage(ChatRole.Assistant, "body");
			int defaultBottom = chat.PanelBottomMarginForTest(id);
			Assert.True(defaultBottom > 0, "precondition: a footerless message keeps its role bottom margin");

			chat.SetMessageRail(id, true);
			Assert.Equal(defaultBottom, chat.PanelBottomMarginForTest(id));
		}

		/// <summary>
		/// The global switch still wins: MessageRailEnabled=false means no rails anywhere, whatever an
		/// individual message asked for.
		/// </summary>
		[Fact]
		public void MessageRailEnabledFalse_OverridesAnExplicitPerMessageRail()
		{
			var (_, chat) = Host();
			var id = chat.AddMessage(ChatRole.Assistant, "body");
			chat.SetMessageRail(id, true);
			Assert.Equal(chat.MessageRailGutterWidth, chat.BodyLeftMarginForTest(id));

			chat.MessageRailEnabled = false;
			Assert.Equal(0, chat.BodyLeftMarginForTest(id));
		}

		/// <summary>
		/// The rail must stop at the last body row. A footerless message keeps its role bottom margin (the
		/// gap before the next message) and the panel slot's Height INCLUDES that margin — so a rail that
		/// spans the whole slot paints one row too far, into the blank gap. A footered message never showed
		/// this because ApplyGutter collapses the panel's bottom margin to 0 when footer rows follow it.
		/// </summary>
		[Fact]
		public void RailedFooterlessMessage_RailStopsAtLastBodyRow_NotTheGapBelow()
		{
			var (system, chat) = Host();
			var id = chat.AddMessage(ChatRole.Assistant, "line one\nline two");
			chat.SetMessageRail(id, true);
			system.Render.UpdateDisplay(); system.Render.UpdateDisplay();

			var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
			int lastBodyRow = RowContaining(snap, "line two");
			Assert.True(lastBodyRow >= 0, "last body row not found");

			// The last body row is railed...
			Assert.Equal("│", snap.GetCell(0, lastBodyRow).Character.ToString());
			// ...and the blank gap row directly below it is NOT.
			Assert.NotEqual("│", snap.GetCell(0, lastBodyRow + 1).Character.ToString());
			System.GC.KeepAlive(system);
		}

		/// <summary>
		/// Existing transcripts are untouched: with no override set, railing still follows footer presence.
		/// </summary>
		[Fact]
		public void WithoutAnOverride_RailStillFollowsFooterPresence()
		{
			var (_, chat) = Host();
			var plain = chat.AddMessage(ChatRole.Assistant, "plain");
			var footered = chat.AddMessage(ChatRole.Assistant, "footered");
			chat.SetStatus(footered, "done");

			Assert.Equal(0, chat.BodyLeftMarginForTest(plain));
			Assert.Equal(chat.MessageRailGutterWidth, chat.BodyLeftMarginForTest(footered));
		}
	}
}
