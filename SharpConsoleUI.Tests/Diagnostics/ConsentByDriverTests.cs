// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Diagnostics.Socket;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.ConsentRules
{
	/// <summary>
	/// Who gets asked before a diagnostics socket opens.
	/// </summary>
	/// <remarks>
	/// The prompt exists for one scenario: an ordinary app on a terminal inherits
	/// the variable from somewhere, and a channel appears that reads the screen
	/// and types into it. Only the user can judge that, and the user is there.
	/// <para>
	/// A headless app is not that scenario. It has no terminal, nothing to look
	/// over your shoulder at, and it is headless because someone wrote
	/// <c>new HeadlessConsoleDriver(...)</c> in code - a compiled-in statement
	/// that this runs unattended. Prompting a user who by construction is not
	/// there, and denying when they do not answer, protects nobody.
	/// </para>
	/// </remarks>
	// Mutates process-wide environment variables, so it cannot run beside
	// anything else that does. Without this the two classes race and the
	// loser reads the other's variable - a CI red that hides behind lucky
	// scheduling in a full-suite run.
	[Collection("EnvSerial")]
	public class ConsentByDriverTests
	{
		private const string SocketVar = "SHARPCONSOLEUI_DIAGNOSTICS_SOCKET";

		/// <summary>Runs <paramref name="body"/> with the variable set, then restores it.</summary>
		private static void WithSocketVar(string? value, Action body)
		{
			string? before = Environment.GetEnvironmentVariable(SocketVar);
			try
			{
				Environment.SetEnvironmentVariable(SocketVar, value);
				body();
			}
			finally
			{
				Environment.SetEnvironmentVariable(SocketVar, before);
			}
		}

		[Fact]
		public void AHeadlessDriverHonoursTheVariableWithoutPrompting()
		{
			WithSocketVar("/tmp/cxdbg-test.sock", () =>
			{
				using var driver = new HeadlessConsoleDriver(80, 24);

				var decision = DiagnosticsConsent.Resolve(null, driver);

				Assert.True(decision.Allowed, decision.DenialReason);
				Assert.Equal("/tmp/cxdbg-test.sock", decision.SocketPath);
			});
		}

		[Fact]
		public void AHeadlessDriverHonoursAnExplicitPathNotJustOne()
		{
			// The whole reason a debugger sets the variable: it picks the path, so
			// each run gets its own socket rather than sharing one.
			WithSocketVar("/tmp/cxdbg-explicit.sock", () =>
			{
				using var driver = new HeadlessConsoleDriver(80, 24);

				var decision = DiagnosticsConsent.Resolve(null, driver);

				Assert.Equal("/tmp/cxdbg-explicit.sock", decision.SocketPath);
			});
		}

		[Fact]
		public void AnOptionPathStillNeedsNoDriver()
		{
			// The in-code route was always allowed without a prompt, by someone who
			// wrote it there. Adding the driver parameter must not disturb it.
			WithSocketVar(null, () =>
			{
				var decision = DiagnosticsConsent.Resolve("/tmp/x.sock", null);

				Assert.True(decision.Allowed, decision.DenialReason);
				Assert.Equal("/tmp/x.sock", decision.SocketPath);
			});
		}

		[Fact]
		public void NothingRequestedIsStillNotADenialWorthReporting()
		{
			// Nobody asked for a socket, so there is nothing to deny and nothing to
			// log. A denial reason here would be noise in every headless app.
			WithSocketVar(null, () =>
			{
				using var driver = new HeadlessConsoleDriver(80, 24);

				var decision = DiagnosticsConsent.Resolve(null, driver);

				Assert.False(decision.Allowed);
				Assert.Null(decision.DenialReason);
			});
		}
	}
}
