// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json;
using Cxdbg.Protocol;

namespace Cxdbg.Launch;

/// <summary>A command cxdbg started, so it can start it again.</summary>
/// <param name="App">The application.</param>
/// <param name="Args">Its own arguments.</param>
internal sealed record RememberedRun(string App, string[] Args);

/// <summary>
/// Remembers the last thing cxdbg started.
/// </summary>
/// <remarks>
/// <para>
/// This is the honest version of "pick the app up after a restart". A restarted
/// application is a different process at a different socket, and nothing links
/// it to the one before except the command that launched it. Watching a
/// directory for a new socket and deciding which one is "the same app" would be
/// a heuristic - wrong sometimes, and wrong silently, which is worse in a
/// debugger than being wrong loudly.
/// </para>
/// <para>
/// So the command is what is remembered, and re-running is re-running: a new
/// process, said out loud, not the old one appearing to come back.
/// </para>
/// </remarks>
internal static class LastRun
{
	/// <summary>Overrides where the record lives. For tests.</summary>
	private const string StateDirVar = "CXDBG_STATE_DIR";

	/// <summary>Writes down what was just started.</summary>
	/// <param name="app">The application.</param>
	/// <param name="args">Its own arguments.</param>
	internal static void Remember(string app, IReadOnlyList<string> args)
	{
		try
		{
			string dir = StateDirectory();
			Directory.CreateDirectory(dir);
			File.WriteAllText(
				Path.Combine(dir, "last-run.json"),
				JsonSerializer.Serialize(new RememberedRun(app, args.ToArray())));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// Remembering is a convenience. Failing to write it down is not a
			// reason to fail a launch that already worked.
		}
	}

	/// <summary>What was started last, or null when nothing was.</summary>
	internal static RememberedRun? Recall()
	{
		try
		{
			string path = Path.Combine(StateDirectory(), "last-run.json");
			if (!File.Exists(path)) return null;

			return JsonSerializer.Deserialize<RememberedRun>(File.ReadAllText(path));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
			return null;
		}
	}

	/// <summary>What was started last, refusing when there is nothing.</summary>
	/// <exception cref="CxdbgException">When nothing has been run yet.</exception>
	internal static RememberedRun Require() =>
		Recall() ?? throw CxdbgException.Usage(
			"nothing to run again: cxdbg has not started an application yet. "
			+ "Start one with `cxdbg run <app> -- <its args>`.");

	/// <summary>Where the record lives.</summary>
	private static string StateDirectory()
	{
		string? overridden = Environment.GetEnvironmentVariable(StateDirVar);
		if (!string.IsNullOrEmpty(overridden)) return overridden;

		string? home = Environment.GetEnvironmentVariable("XDG_STATE_HOME");

		return string.IsNullOrEmpty(home)
			? Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				".local", "state", "cxdbg")
			: Path.Combine(home, "cxdbg");
	}
}
