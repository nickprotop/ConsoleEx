// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Telling "nothing yet" apart from "this broke".
	/// </summary>
	/// <remarks>
	/// EmptyText covers a chart that is working and waiting. A feed that has
	/// DIED looks exactly the same to the reader, which is the failure mode a
	/// monitor actually hits: the chart says "no samples received yet" forever
	/// and nobody learns the collector crashed.
	/// </remarks>
	public class ChartErrorStateTests
	{
		private static (ConsoleWindowSystem System, ChartControl Chart) Build()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(60, 16);
			var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 16 };

			var chart = new ChartControl
			{
				Name = "c",
				PlotHeight = 8,
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};

			window.AddControl(chart);
			system.AddWindow(window);
			return (system, chart);
		}

		private static CharacterBufferSnapshot Draw(ConsoleWindowSystem system)
		{
			system.Render.UpdateDisplay();
			system.Render.UpdateDisplay();
			return system.RenderingDiagnostics!.LastBufferSnapshot!;
		}

		private static string TextOf(CharacterBufferSnapshot snap)
		{
			var sb = new System.Text.StringBuilder();
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
					sb.Append((char)snap.GetCell(x, y).Character.Value);
				sb.Append('\n');
			}
			return sb.ToString();
		}

		private static bool HasColour(CharacterBufferSnapshot snap, Color want)
		{
			for (int y = 0; y < snap.Height; y++)
				for (int x = 0; x < snap.Width; x++)
					if (snap.GetCell(x, y).Foreground == want) return true;
			return false;
		}

		[Fact]
		public void AnErrorIsShownInsteadOfTheEmptyMessage()
		{
			var (system, chart) = Build();
			chart.EmptyText = "waiting for samples";
			chart.ErrorText = "collector offline";

			string text = TextOf(Draw(system));

			Assert.Contains("collector offline", text);
			Assert.DoesNotContain("waiting for samples", text);
		}

		[Fact]
		public void AnErrorReadsDifferentlyFromAnEmptyChart()
		{
			// Same words would not help: the point is that a reader can tell the
			// two apart at a glance, which means a different colour.
			var (errSys, err) = Build();
			err.ErrorText = "collector offline";
			var errSnap = Draw(errSys);

			Assert.True(HasColour(errSnap, ControlDefaults.ChartErrorTextColor),
				"the error message did not use the error colour");
			Assert.False(HasColour(errSnap, ControlDefaults.ChartEmptyTextColor),
				"the error message used the empty-state colour");
		}

		[Fact]
		public void AnErrorOutranksDataAsWellAsEmptiness()
		{
			// A stale trace under an error banner invites reading the stale trace
			// as current. The error replaces the plot, it does not decorate it.
			var (system, chart) = Build();
			chart.AddSeries("v", Color.Green).SetValues(new[] { 10d, 20, 30, 40 });
			chart.ErrorText = "collector offline";

			var snap = Draw(system);

			Assert.Contains("collector offline", TextOf(snap));

			int inked = 0;
			for (int y = 0; y < snap.Height; y++)
				for (int x = 0; x < snap.Width; x++)
				{
					char ch = (char)snap.GetCell(x, y).Character.Value;
					if (ch == '█' || ch == '▀' || ch == '▄') inked++;
				}
			Assert.Equal(0, inked);
		}
	}
}
