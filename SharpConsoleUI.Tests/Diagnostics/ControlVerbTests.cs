// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using System.Text.Json.Nodes;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Socket;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.ControlLookup
{
	/// <summary>
	/// Asking about one control.
	/// </summary>
	/// <remarks>
	/// Four verbs take a control and all four act on it or explain one axis of
	/// it. Reading what a control IS meant fetching the whole tree with props
	/// and walking it - the reply grows with the application, for one answer.
	/// </remarks>
	public class ControlVerbTests
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
				.WithSize(40, 12)
				.AddControl(SharpConsoleUI.Builders.Controls.Markup("hello").WithName("greeting").Build())
				.BuildAndShow();

			window.RenderAndGetVisibleContent();
			return system;
		}

		[Fact]
		public void ItAnswersByName()
		{
			var system = Build(out var window);

			var reply = DiagnosticsSerializer.Control(window, "greeting");

			Assert.NotNull(reply);
			Assert.Equal("greeting", reply!["name"]!.GetValue<string>());
			Assert.Equal("MarkupControl", reply["control"]!.GetValue<string>());
		}

		[Fact]
		public void ItAnswersByTheIdTreeReports()
		{
			// So a caller can hand back exactly what it was given, without the
			// control having been named.
			var system = Build(out var window);

			string id = DiagnosticsSerializer.Control(window, "greeting")!["id"]!.GetValue<string>();

			Assert.NotNull(DiagnosticsSerializer.Control(window, id));
		}

		[Fact]
		public void ItCarriesWhatTheTreeWouldSayAboutIt()
		{
			// The same shape as a tree node, because a second shape for the
			// same control would be a second thing to keep in step.
			Build(out var window);

			var reply = DiagnosticsSerializer.Control(window, "greeting")!;

			Assert.NotNull(reply["bounds"]);
			Assert.NotNull(reply["desired"]);
			Assert.NotNull(reply["props"]);
		}

		[Fact]
		public void ItCarriesTheChainToTheRoot()
		{
			// The part a node alone cannot give, and the reason the whole tree
			// was being fetched: what contains this, and what contains that.
			var system = Build(out var window);

			var chain = DiagnosticsSerializer.Control(window, "greeting")!["parents"]!.AsArray();

			Assert.NotEmpty(chain);

			// Outermost first, the way a cascade reads.
			Assert.Equal("(root)", chain[0]!["control"]!.GetValue<string>());
		}

		[Fact]
		public void ItHasNoChildrenOfItsOwnToWalk()
		{
			// `control` answers about ONE control. A caller wanting the subtree
			// asks tree, which is the verb for that.
			var system = Build(out var window);

			Assert.Null(DiagnosticsSerializer.Control(window, "greeting")!["children"]);
		}

		[Fact]
		public void AnUnknownControlIsNull()
		{
			var system = Build(out var window);

			Assert.Null(DiagnosticsSerializer.Control(window, "nosuchcontrol"));
		}

		[Fact]
		public void ItAnswersByTheBareTypeToo()
		{
			// The type is what `tree` shows and what a reader types first, and
			// `measure` already takes it. A verb that accepted one spelling and
			// not the others would be the odd one out.
			Build(out var window);

			Assert.NotNull(DiagnosticsSerializer.Control(window, "MarkupControl"));
		}

		[Fact]
		public void AFieldSentWithoutItsValueDoesNotAnswerWithAnException()
		{
			// `cxdbg measure --control` with the value left off: a bare flag is
			// true, which is right, and the server then called GetValue<string>
			// on it and THREW. The handler's catch turned the exception into the
			// reply, so a person who mistyped a command was told
			// "InvalidOperationException: An element of type 'True' cannot be
			// converted to a 'System.String'" - which names no field and suggests
			// nothing.
			var req = new JsonObject { ["cmd"] = "measure", ["control"] = true };

			// Read for what it says rather than how it was typed. "true" matches
			// no control, so the verb gives its own "nothing matched" - the right
			// answer, from the code that knows what was being looked for.
			Assert.Equal("true", DiagnosticsServer.TextForTest(req, "control"));
		}

		[Fact]
		public void AMissingFieldReadsAsNothingRatherThanThrowing()
		{
			var req = new JsonObject { ["cmd"] = "measure" };

			Assert.Null(DiagnosticsServer.TextForTest(req, "control"));
		}

		[Fact]
		public void AnEmptyFieldReadsAsNothingSoTheDefaultStillApplies()
		{
			// --window "" asked for the window named empty string, matched none,
			// and reported no window rather than using the active one. Absent and
			// empty mean the same thing to a command line.
			var req = new JsonObject { ["cmd"] = "control", ["window"] = "" };

			Assert.Null(DiagnosticsServer.TextForTest(req, "window"));
		}

		[Fact]
		public void ANumericFieldStillReadsAsItsDigits()
		{
			// A control legitimately named "42", or a window id sent unquoted by
			// a client that typed it as a number. Neither should throw, and both
			// should look up what the caller wrote.
			var req = new JsonObject { ["cmd"] = "control", ["control"] = 42 };

			Assert.Equal("42", DiagnosticsServer.TextForTest(req, "control"));
		}

		[Fact]
		public void AStringFieldIsUnchanged()
		{
			var req = new JsonObject { ["cmd"] = "control", ["control"] = "okButton" };

			Assert.Equal("okButton", DiagnosticsServer.TextForTest(req, "control"));
		}

		[Fact]
		public void AnActionFieldMayNameTheWindowItActsOn()
		{
			// {"activate":"Settings"} is how the verb is documented and the only
			// spelling that reads on a command line. The handler read the field with
			// GetValue<bool>, which THREW on a string, and the catch turned that
			// into the reply - so the documented form answered
			// "InvalidOperationException: An element of type 'String' cannot be
			// converted to a 'System.Boolean'".
			var req = new JsonObject { ["cmd"] = "window", ["activate"] = "Settings" };

			Assert.True(DiagnosticsServer.AskedForTest(req, "activate"));
			Assert.Equal("Settings", DiagnosticsServer.ActionForTest(req, "activate"));
		}

		[Fact]
		public void ABareFlagStillAsksForTheAction()
		{
			// The other spelling: {"window":"Settings","activate":true}.
			var req = new JsonObject { ["cmd"] = "window", ["activate"] = true };

			Assert.True(DiagnosticsServer.AskedForTest(req, "activate"));

			// A flag names no window, so the 'window' field decides which one.
			Assert.Null(DiagnosticsServer.ActionForTest(req, "activate"));
		}

		[Fact]
		public void AnExplicitFalseDoesNotAskForTheAction()
		{
			var req = new JsonObject { ["cmd"] = "window", ["activate"] = false };

			Assert.False(DiagnosticsServer.AskedForTest(req, "activate"));
		}

		[Fact]
		public void AnAbsentActionFieldAsksForNothing()
		{
			var req = new JsonObject { ["cmd"] = "window" };

			Assert.False(DiagnosticsServer.AskedForTest(req, "activate"));
			Assert.Null(DiagnosticsServer.ActionForTest(req, "activate"));
		}
	}
}
