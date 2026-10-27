// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Surfaces;
using Cell = SharpConsoleUI.Layout.Cell;
using Spectre.Console;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// One headless application showing another headless application's screen.
	/// </summary>
	/// <remarks>
	/// This is the whole path a remote view takes: an application paints, its
	/// surface is exported as markup, the markup crosses a wire, a second
	/// application parses it back and paints it. Every step is lossless or the
	/// picture is wrong, and the only way to know is to compare the two screens
	/// cell by cell.
	/// <para>
	/// The individual pieces are tested elsewhere. What this adds is that they
	/// compose - which is the thing that is actually being claimed.
	/// </para>
	/// </remarks>
	public class SurfaceRoundTripTests
	{
		private static ConsoleWindowSystem Headless(int w, int h, out HeadlessConsoleDriver driver)
		{
			driver = new HeadlessConsoleDriver(w, h);
			return new ConsoleWindowSystem(driver,
				options: ConsoleWindowSystemOptions.Create() with
				{
					PipedInput = new PipedInputOptions(Enabled: false),
				});
		}

		[Fact]
		public void AViewerPaintsWhatTheSourceApplicationPainted()
		{
			// ---- the application being looked at -------------------------
			var source = Headless(40, 8, out _);
			var sourceWindow = new Window(source)
			{
				Left = 0,
				Top = 0,
				Width = 30,
				Height = 5,
				BorderStyle = BorderStyle.Frameless,
			};

			sourceWindow.AddControl(new MarkupControl(new()
			{
				"[bold red]Totals[/] 日本語",
				"[blue on yellow]second[/] line",
			}));

			source.AddWindow(sourceWindow);
			sourceWindow.RenderAndGetVisibleContent();

			// ---- what crosses the wire -----------------------------------
			var sourceSurface = sourceWindow.CaptureSurface();
			string markup = sourceSurface.ToText(TextFormat.Markup);

			var rows = new List<IReadOnlyList<Cell>>();
			foreach (string line in markup.Split('\n'))
				rows.Add(MarkupParser.Parse(line, Color.White, Color.Black));

			// ---- the application doing the looking -----------------------
			var viewer = Headless(40, 8, out _);
			var viewerWindow = new Window(viewer)
			{
				Left = 0,
				Top = 0,
				Width = 30,
				Height = 5,
				BorderStyle = BorderStyle.Frameless,
			};

			var view = new SurfaceViewControl();
			view.SetSurface(rows);
			viewerWindow.AddControl(view);

			viewer.AddWindow(viewerWindow);
			viewerWindow.RenderAndGetVisibleContent();

			// ---- the two screens are the same ----------------------------
			var viewerSurface = viewerWindow.CaptureSurface();

			int compared = 0;
			for (int y = 0; y < sourceSurface.Height && y < viewerSurface.Height; y++)
			{
				for (int x = 0; x < sourceSurface.Width && x < viewerSurface.Width; x++)
				{
					var a = sourceSurface[x, y];
					var b = viewerSurface[x, y];

					Assert.Equal(a.Character.Value, b.Character.Value);
					Assert.Equal(a.Foreground, b.Foreground);
					Assert.Equal(a.Background, b.Background);
					Assert.Equal(a.Decorations, b.Decorations);
					Assert.Equal(a.IsWideContinuation, b.IsWideContinuation);
					compared++;
				}
			}

			Assert.True(compared > 100, $"only {compared} cells compared; the screens were empty");
		}
	}
}
