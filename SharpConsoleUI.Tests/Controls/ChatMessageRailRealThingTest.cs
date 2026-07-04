// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	public class ChatMessageRailRealThingTest
	{
		[Fact]
		public void Rail_SurvivesCollapse_SpansFooter_HeaderStaysRailless()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(44, 18);
			var window = new Window(system) { Left = 0, Top = 0, Width = 44, Height = 18 };
			var chat = new ChatTranscriptControl { VerticalAlignment = VerticalAlignment.Fill };
			window.AddControl(chat);
			system.AddWindow(window);

			// Tool role is collapsible + starts collapsed. Multi-line content + a status row (footer).
			var id = chat.AddMessage(ChatRole.Tool, "tool output line one\nline two\nline three");
			chat.SetStatus(id, "ok");
			system.Render.UpdateDisplay();
			system.Render.UpdateDisplay();

			var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;

			// --- Collapsed: rail present on the footer row(s), header is railless. ---
			int collapsedRailRows = CountRailRows(snap);
			int headerRow = FindHeaderRow(snap);

			Assert.True(collapsedRailRows > 0, "rail did not survive on the collapsed message's footer");
			Assert.True(headerRow >= 0, "collapsed header row (tool label) not found");
			Assert.NotEqual("│", snap.GetCell(0, headerRow).Character.ToString());

			// --- Expand: rail now spans body + footer (more rows), header still railless. ---
			chat.PanelForTest(id).IsExpanded = true;
			system.Render.UpdateDisplay();
			system.Render.UpdateDisplay();

			snap = system.RenderingDiagnostics!.LastBufferSnapshot!;

			int expandedRailRows = CountRailRows(snap);
			int headerRowExpanded = FindHeaderRow(snap);

			Assert.True(
				expandedRailRows > collapsedRailRows,
				$"expanding must extend the rail over the body: collapsed={collapsedRailRows}, expanded={expandedRailRows}");
			Assert.True(headerRowExpanded >= 0, "expanded header row (tool label) not found");
			Assert.NotEqual("│", snap.GetCell(0, headerRowExpanded).Character.ToString());

			System.GC.KeepAlive(system);
		}

		private static int CountRailRows(CharacterBufferSnapshot snap)
		{
			int rows = 0;
			for (int y = 0; y < snap.Height; y++)
				if (snap.GetCell(0, y).Character.ToString() == "│")
					rows++;
			return rows;
		}

		private static int FindHeaderRow(CharacterBufferSnapshot snap)
		{
			for (int y = 0; y < snap.Height; y++)
			{
				string row = RowText(snap, y);
				if (row.Contains("tool") || row.Contains("🔧"))
					return y;
			}
			return -1;
		}

		private static string RowText(CharacterBufferSnapshot snap, int y)
		{
			var sb = new System.Text.StringBuilder();
			for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character.ToString());
			return sb.ToString();
		}
	}
}
