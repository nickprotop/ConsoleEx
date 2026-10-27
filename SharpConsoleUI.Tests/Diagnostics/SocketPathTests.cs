// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Runtime.InteropServices;
using SharpConsoleUI.Diagnostics.Socket;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.SocketPaths
{
	/// <summary>
	/// Where a generated socket lands.
	/// </summary>
	/// <remarks>
	/// The socket file is owner-only where the platform supports it, but the
	/// directory holding it matters too: dropping it straight into a shared
	/// temp directory tells every other user on the machine that this app is
	/// running and what its process id is, even though they cannot connect.
	/// </remarks>
	public class SocketPathTests
	{
		private const string SocketVar = "SHARPCONSOLEUI_DIAGNOSTICS_SOCKET";
		private const string RuntimeVar = "XDG_RUNTIME_DIR";

		private static void WithVars(string? socket, string? runtime, Action body)
		{
			string? beforeSocket = Environment.GetEnvironmentVariable(SocketVar);
			string? beforeRuntime = Environment.GetEnvironmentVariable(RuntimeVar);
			try
			{
				Environment.SetEnvironmentVariable(SocketVar, socket);
				Environment.SetEnvironmentVariable(RuntimeVar, runtime);
				body();
			}
			finally
			{
				Environment.SetEnvironmentVariable(SocketVar, beforeSocket);
				Environment.SetEnvironmentVariable(RuntimeVar, beforeRuntime);
			}
		}

		[Fact]
		public void WithNoRuntimeDirTheSocketDoesNotLandDirectlyInSharedTemp()
		{
			WithVars(socket: "1", runtime: null, () =>
			{
				using var driver = new HeadlessConsoleDriver(80, 24);

				var decision = DiagnosticsConsent.Resolve(null, driver);

				Assert.True(decision.Allowed, decision.DenialReason);

				string dir = Path.GetDirectoryName(decision.SocketPath!)!;
				string temp = Path.TrimEndingDirectorySeparator(Path.GetTempPath());

				Assert.NotEqual(temp, Path.TrimEndingDirectorySeparator(dir));
				Assert.StartsWith(temp, dir);
			});
		}

		[Fact]
		public void TheGeneratedDirectoryIsOwnerOnlyWhereThatMeansSomething()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

			WithVars(socket: "1", runtime: null, () =>
			{
				using var driver = new HeadlessConsoleDriver(80, 24);

				var decision = DiagnosticsConsent.Resolve(null, driver);
				string dir = Path.GetDirectoryName(decision.SocketPath!)!;

				var mode = File.GetUnixFileMode(dir);

				Assert.Equal(UnixFileMode.None, mode & (UnixFileMode.GroupRead
					| UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
					| UnixFileMode.OtherRead | UnixFileMode.OtherWrite
					| UnixFileMode.OtherExecute));
			});
		}

		[Fact]
		public void AnUnusableRuntimeDirIsDeniedRatherThanThrown()
		{
			// Resolve runs during construction. A path that cannot be created must
			// become a denial the app logs, not an exception that stops it starting.
			string unusable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? "\\\\?\\Z:\\nope\\nowhere"
				: "/proc/self/mem/nope";

			WithVars(socket: "1", runtime: unusable, () =>
			{
				using var driver = new HeadlessConsoleDriver(80, 24);

				var decision = DiagnosticsConsent.Resolve(null, driver);

				Assert.False(decision.Allowed);
				Assert.NotNull(decision.DenialReason);
			});
		}
	}
}
