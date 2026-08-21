// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Diagnostics;
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
		public static void Open(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				return;

			if (!IsLaunchableScheme(url))
				return;

			// Off the calling thread: both ShellExecuteEx and xdg-open hand off to the desktop shell,
			// which can stall on a slow or missing handler. Same rule ClipboardHelper.SetText follows
			// for process-backed I/O — never block the UI thread on something the desktop controls.
			ThreadPool.QueueUserWorkItem(_ => LaunchBestEffort(url));
		}

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
			try
			{
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			}
			catch
			{
				try
				{
					if (OperatingSystem.IsMacOS())
						Process.Start("open", url);
					else if (OperatingSystem.IsLinux())
						Process.Start("xdg-open", url);
					// Windows: UseShellExecute already covers it; nothing more to try.
				}
				catch
				{
					// Opening a browser is a convenience; there is nothing to recover if it fails.
				}
			}
		}
	}
}
