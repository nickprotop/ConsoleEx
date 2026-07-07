// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;

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
		public static void Open(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				return;

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
