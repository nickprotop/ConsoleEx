// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Opens URLs in the platform's default browser. Best-effort: pairs with the framework's markup and
	/// markdown link support so a clicked link can actually be opened. Never throws.
	/// </summary>
	public static class UrlLauncher
	{
		/// <summary>
		/// Opens <paramref name="url"/> in the platform's default browser. Best-effort — swallows all
		/// exceptions (launching a browser is a convenience; a failure has nothing to recover). A null or
		/// whitespace url is a safe no-op.
		/// </summary>
		/// <param name="url">The URL to open. Null or whitespace is ignored.</param>
		/// <remarks>
		/// Returns immediately: the launch runs on the thread pool, so a slow or blocking desktop shell
		/// cannot stall the caller. Callers are typically the UI thread handling a click on a link, and a
		/// frozen UI is a far worse outcome than a link that did not open.
		///
		/// <para>This matters on Windows specifically. <c>UseShellExecute</c> reaches
		/// <c>ShellExecuteEx</c>, which answers a scheme with no registered handler by showing a modal
		/// "How do you want to open this file?" dialog and waiting on it — indefinitely on an unattended
		/// machine, and behind the terminal window on an attended one. Found on a real desktop, where it
		/// froze a whole test run ~180 tests from the end.</para>
		/// </remarks>
		public static void Open(string url) => TryOpen(url);

		/// <summary>
		/// Opens <paramref name="url"/> and reports whether anything was launched.
		/// </summary>
		/// <param name="url">The URL to open. Null or whitespace is ignored.</param>
		/// <returns>
		/// False when the url is empty, its scheme is not launchable, or there is no desktop to
		/// launch into. True when the launch was handed off, which is not a promise that it opened:
		/// it runs on the thread pool and the desktop shell decides the rest.
		/// </returns>
		/// <remarks>
		/// For a caller with something else to offer. A terminal application that cannot open an
		/// image in a viewer can print the path instead, but only if it knows the viewer never
		/// started. <see cref="Open"/> cannot say, so every caller that cared had to repeat the
		/// desktop check itself.
		/// </remarks>
		public static bool TryOpen(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				return false;

			if (!IsLaunchableScheme(url))
				return false;

			// NO DESKTOP, NO LAUNCH. With neither DISPLAY nor WAYLAND_DISPLAY, xdg-open falls back
			// to a text browser - w3m, lynx - which takes over the terminal this application is
			// drawing in. The user is left looking at a browser they did not ask for, over a UI
			// that is still rendering underneath it.
			if (!CanReachADesktop())
				return false;

			// Off the calling thread: both ShellExecuteEx and xdg-open hand off to the desktop shell,
			// which can stall on a slow or missing handler. Same rule ClipboardHelper.SetText follows
			// for process-backed I/O — never block the UI thread on something the desktop controls.
			ThreadPool.QueueUserWorkItem(_ => LaunchBestEffort(url));
			return true;
		}

		/// <summary>Whether a desktop exists to hand a url to.</summary>
		/// <remarks>
		/// Linux only. Windows and macOS always have one. The Wayland test matches the clipboard's:
		/// libwayland connects to the default socket when the variable is unset, so a shell that
		/// lost it (sudo -E, an old tmux server) still has a desktop.
		/// </remarks>
		internal static bool CanReachADesktop()
		{
			if (!OperatingSystem.IsLinux())
				return true;

			if (DesktopProbeForTests is { } probe)
				return probe();

			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
				return true;

			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
				return true;

			string? runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
			return !string.IsNullOrEmpty(runtime) && File.Exists(Path.Combine(runtime, "wayland-0"));
		}

		/// <summary>Replaces the desktop check. For tests, which cannot have a display either way.</summary>
		internal static Func<bool>? DesktopProbeForTests { get; set; }

		/// <summary>Replaces the process start, so a test can see what would have been launched.</summary>
		internal static Action<ProcessStartInfo>? StartProbeForTests { get; set; }

		/// <summary>
		/// URI schemes <see cref="Open"/> will launch, beyond the always-allowed
		/// <c>http</c>, <c>https</c>, <c>mailto</c> and <c>file</c>.
		/// </summary>
		/// <remarks>
		/// Application schemes such as <c>vscode://</c>, <c>slack://</c> or <c>obsidian://</c> launch by
		/// default, because that has always worked and an application relying on it should not lose it.
		///
		/// <para>The cost is that an unregistered scheme reaches the desktop shell, and on Windows that
		/// means the "How do you want to open this file?" dialog — no longer able to freeze the caller,
		/// since the launch is off-thread, but still a dialog the user must dismiss. An application that
		/// only ever opens web links can rule that out entirely by assigning a fixed set:</para>
		///
		/// <example>
		/// <code>
		/// // Web links only — an unknown scheme is dropped instead of reaching the shell.
		/// UrlLauncher.AllowedSchemes = Array.Empty&lt;string&gt;();
		///
		/// // Or the app's own scheme, and nothing else.
		/// UrlLauncher.AllowedSchemes = new[] { "vscode" };
		/// </code>
		/// </example>
		///
		/// <para>Null (the default) means "any scheme". Comparison is case-insensitive.</para>
		/// </remarks>
		public static IReadOnlyCollection<string>? AllowedSchemes { get; set; }

		/// <summary>
		/// True when <paramref name="url"/> is absolute and its scheme is one this helper will launch.
		/// </summary>
		internal static bool IsLaunchableScheme(string url)
		{
			// A relative or malformed string is not launchable by anything, and is the other input that
			// used to reach the shell ("not a url at all", "/just/a/path").
			if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
				return false;

			// Always allowed: the schemes a browser or mail client handles, which is what this helper is for.
			if (uri.Scheme == Uri.UriSchemeHttp
				|| uri.Scheme == Uri.UriSchemeHttps
				|| uri.Scheme == Uri.UriSchemeMailto
				|| uri.Scheme == Uri.UriSchemeFile)
				return true;

			var allowed = AllowedSchemes;
			if (allowed == null)
				return true; // Default: preserve the historical behaviour for application schemes.

			foreach (var scheme in allowed)
			{
				if (string.Equals(scheme, uri.Scheme, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		/// <summary>Hands the url to the platform launcher, swallowing every failure.</summary>
		private static void LaunchBestEffort(string url)
		{
			// WINDOWS KEEPS UseShellExecute: it is how a scheme reaches its registered handler, and
			// a Windows console is not the surface a launched program writes over.
			if (OperatingSystem.IsWindows())
			{
				try { Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
				catch { /* a convenience; nothing to recover */ }
				return;
			}

			// EVERYWHERE ELSE THE STREAMS ARE TAKEN. The launcher and whatever it starts inherit
			// this process's terminal otherwise, and that terminal is the screen the UI is drawing
			// on: xdg-open's own warnings land in the middle of it, and a handler that turns out to
			// be a text browser takes it over completely.
			string launcher = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
			var info = new ProcessStartInfo(launcher)
			{
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
			};
			info.ArgumentList.Add(url);

			try
			{
				var process = Start(info);
				if (process == null)
					return;

				// Closed rather than read: nothing here wants the output, and a pipe nobody drains
				// fills and blocks the child once it has printed enough.
				process.StandardInput.Close();
				process.BeginOutputReadLine();
				process.BeginErrorReadLine();
			}
			catch
			{
				// Opening a browser is a convenience; there is nothing to recover if it fails.
			}
		}

		/// <summary>Starts a process, or hands it to the test probe instead.</summary>
		private static Process? Start(ProcessStartInfo info)
		{
			if (StartProbeForTests is { } probe)
			{
				probe(info);
				return null;
			}
			return Process.Start(info);
		}
	}
}
