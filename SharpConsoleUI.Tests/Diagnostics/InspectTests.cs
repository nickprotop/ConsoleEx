// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using System.Linq;
using System.Text.Json.Nodes;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Diagnostics.Socket;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.PointInspection
{
	/// <summary>
	/// What is under a point.
	/// </summary>
	/// <remarks>
	/// The devtools inspector: you are looking at a screenshot, something is
	/// wrong THERE, and you have coordinates rather than a name. The answer is
	/// layered on purpose - the interesting one is often not the topmost thing,
	/// because "the click does nothing" is usually "you are hitting the window
	/// border, not the button".
	/// </remarks>
	public class InspectTests
	{
		private static ConsoleWindowSystem Build(out Window window)
		{
			var system = new ConsoleWindowSystem(new HeadlessConsoleDriver(60, 20),
				options: ConsoleWindowSystemOptions.Create() with
				{
					PipedInput = new PipedInputOptions(Enabled: false),
				});

			window = new WindowBuilder(system)
				.WithTitle("Settings")
				.WithSize(30, 10)
				.AtPosition(5, 3)
				.AddControl(SharpConsoleUI.Builders.Controls.Markup("hello").WithName("greeting").Build())
				.BuildAndShow();

			window.RenderAndGetVisibleContent();
			return system;
		}

		[Fact]
		public void BareDesktopIsAnAnswerNotAnError()
		{
			// "Nothing is there" is the most useful reply when you expected
			// something to be.
			var system = Build(out _);

			// Below the top panel, above the bottom one, clear of the window.
			var reply = DiagnosticsSerializer.Inspect(system, 55, 2);

			Assert.Null(reply["window"]);
			Assert.Equal("desktop", reply["region"]!.GetValue<string>());
		}

		[Fact]
		public void APointInAWindowNamesTheWindow()
		{
			var system = Build(out var window);

			var reply = DiagnosticsSerializer.Inspect(system, 10, 6);

			Assert.Equal("Settings", reply["window"]!["title"]!.GetValue<string>());
		}

		[Fact]
		public void ThePointIsEchoedBack()
		{
			// A caller sweeping points keeps replies straight by the point in
			// them, not by the order they arrive.
			var reply = DiagnosticsSerializer.Inspect(Build(out _), 10, 6);

			Assert.Equal(10, reply["x"]!.GetValue<int>());
			Assert.Equal(6, reply["y"]!.GetValue<int>());
		}

		[Fact]
		public void ItSaysWhatIsInTheCell()
		{
			// The screen's own answer, beside the layout's: when they disagree
			// that disagreement is the bug.
			var reply = DiagnosticsSerializer.Inspect(Build(out _), 10, 6);

			Assert.NotNull(reply["cell"]);
		}

		[Fact]
		public void AWindowsBorderIsNotItsClientArea()
		{
			// The whole reason for the verb: a click that lands on chrome does
			// nothing, and looks exactly like a click that landed on a control
			// that ignored it.
			var system = Build(out var window);

			// The window's own top-left is its border.
			var onBorder = DiagnosticsSerializer.Inspect(system, window.Left, window.Top + 1);

			Assert.NotEqual("client", onBorder["region"]!.GetValue<string>());
		}

		[Fact]
		public void AControlUnderThePointComesWithItsChain()
		{
			// Same shape the control verb gives, so there is no second
			// description of a control to keep in step.
			var system = Build(out var window);

			// Searched across the window's client area rather than computed from
			// the control's own rectangle: a control's bounds are
			// window-relative, and where a click lands is the window's dispatch
			// to decide - which is the thing being tested.
			JsonObject? hit = null;

			for (int dy = 1; dy < window.Height - 1 && hit == null; dy++)
			{
				for (int dx = 1; dx < window.Width - 1 && hit == null; dx++)
				{
					var at = DiagnosticsSerializer.Inspect(system,
						window.Left + dx, window.Top + system.DesktopUpperLeft.Y + dy);

					if (at["control"] is JsonObject there) hit = there;
				}
			}

			var reply = new JsonObject { ["control"] = hit?.DeepClone() };

			Assert.NotNull(reply["control"]);
			Assert.Equal("greeting", reply["control"]!["name"]!.GetValue<string>());
			Assert.NotNull(reply["control"]!["parents"]);
		}
	}
}
