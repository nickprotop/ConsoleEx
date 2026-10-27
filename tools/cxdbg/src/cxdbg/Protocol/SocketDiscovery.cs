// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace Cxdbg.Protocol;

/// <summary>
/// Finds the sockets of applications that are already running.
/// </summary>
/// <remarks>
/// An application asked for a generated path puts its socket in the user's
/// runtime directory, or in an owner-only directory inside temp when there is
/// none. Those are the two places worth looking.
/// </remarks>
public static class SocketDiscovery
{
	/// <summary>The file name an application generates for itself.</summary>
	private const string Pattern = "sharpconsoleui-*.sock";

	/// <summary>Every candidate socket on this machine.</summary>
	/// <param name="extraDirectories">Additional directories to search.</param>
	/// <returns>Paths, most recently written first.</returns>
	public static IReadOnlyList<string> FindCandidates(IEnumerable<string>? extraDirectories = null)
	{
		var dirs = new List<string>();

		string? runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
		if (!string.IsNullOrEmpty(runtime)) dirs.Add(runtime);
		dirs.Add(Path.Combine(Path.GetTempPath(), "sharpconsoleui"));
		if (extraDirectories != null) dirs.AddRange(extraDirectories);

		var found = new List<(string Path, DateTime When)>();

		foreach (string dir in dirs.Distinct())
		{
			if (!Directory.Exists(dir)) continue;

			try
			{
				// A test or a debugger may name a socket itself, so look for
				// anything that ends .sock rather than only the generated shape.
				foreach (string file in Directory.EnumerateFiles(dir, "*.sock"))
					found.Add((file, File.GetLastWriteTimeUtc(file)));
			}
			catch (UnauthorizedAccessException) { /* not ours to read */ }
			catch (IOException) { /* vanished under us */ }
		}

		// Newest first: the app you just started is the one you probably mean.
		return found
			.OrderByDescending(f => f.When)
			.Select(f => f.Path)
			.Distinct()
			.ToList();
	}
}
