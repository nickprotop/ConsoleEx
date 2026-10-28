// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using ScHost.Models;
using Spectre.Console;

namespace ScHost.Terminal;

/// <summary>
/// Linux terminal emulator detection and launch support.
/// </summary>
public static class LinuxTerminal
{
	/// <summary>
	/// Terminal emulators in order of preference.
	/// </summary>
	private static readonly string[] TerminalOrder =
	[
		"ghostty", "kitty", "alacritty", "wezterm", "foot",
		"gnome-terminal", "konsole", "xfce4-terminal", "xterm"
	];

	/// <summary>
	/// Detects the terminal emulator to launch in.
	/// </summary>
	/// <returns>An emulator, or null when there is no desktop to open one on.</returns>
	/// <remarks>
	/// <para>
	/// Null when there is no X or Wayland session. The emulators below are on
	/// PATH whether or not a desktop is running - a package installs them, a
	/// session does not - so looking only at PATH made schost announce
	/// "Launched in kitty" over SSH and in CI while the emulator exited with
	/// "the DISPLAY environment variable is missing" and the app never ran.
	/// The caller falls back to running inline, which is what someone without
	/// a desktop wanted anyway.
	/// </para>
	/// <para>
	/// The user's own choice comes before any list of ours.
	/// <c>xdg-terminal-exec</c> implements the freedesktop Default Terminal
	/// Specification: it runs a command in the terminal THEY picked, which a
	/// hardcoded preference order cannot know. Ours opened kitty on a GNOME
	/// session because kitty sorted earlier.
	/// </para>
	/// <para>
	/// Mirrored in cxdbg's TerminalFinder, which needs the same answer for the
	/// same reason. The two lists are small and stable; a tenth emulator is
	/// added in both.
	/// </para>
	/// </remarks>
	public static string? DetectTerminal(SchostConfig? config = null)
	{
		// Can a window be opened RIGHT NOW? Not the same question as which
		// terminal a .desktop file should name, which is why that caller uses
		// PickTerminal instead.
		if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
			&& string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
		{
			return null;
		}

		return PickTerminal(config);
	}

	/// <summary>
	/// Which terminal to use, without asking whether one can be opened now.
	/// </summary>
	/// <param name="config">The app's configuration, or null when it has none.</param>
	/// <returns>An emulator, or null when none is installed.</returns>
	/// <remarks>
	/// For a .desktop file, which is written now and run later - possibly on a
	/// build machine with no desktop at all. Asking "is there a display"
	/// there would bake xterm into a file that outlives the question.
	/// </remarks>
	public static string? PickTerminal(SchostConfig? config = null)
	{
		// Only when schost has nothing of its own to say about the window.
		// These launchers take a command and no geometry, so a configured
		// size or font would be silently dropped by using one.
		if (!WantsItsOwnWindow(config) && IsCommandAvailable("xdg-terminal-exec"))
			return "xdg-terminal-exec";

		foreach (var name in TerminalOrder)
		{
			if (IsCommandAvailable(name))
				return name;
		}

		return null;
	}

	/// <summary>
	/// Whether the configuration asks for a window only a named emulator can give.
	/// </summary>
	/// <remarks>
	/// Styling the window is schost's job, and the default-terminal launchers
	/// offer no way to set a size or a font. So a configured one means using
	/// an emulator whose flags are known; an unconfigured one means honouring
	/// the user's choice of terminal.
	/// </remarks>
	private static bool WantsItsOwnWindow(SchostConfig? config = null) =>
		config != null
		&& (config.Columns.HasValue || config.Rows.HasValue
			|| config.Font != null || config.FontSize.HasValue);

	/// <summary>
	/// Launches the app in a detected terminal emulator with configured geometry.
	/// </summary>
	public static bool Launch(SchostConfig config, string exePath)
	{
		var terminal = DetectTerminal(config);
		if (terminal is null)
		{
			// Not a warning: no desktop is an ordinary situation, and the
			// caller runs the application inline instead.
			return false;
		}

		var args = BuildLaunchArgs(terminal, config, exePath);

		try
		{
			var psi = new ProcessStartInfo(terminal)
			{
				Arguments = args,
				UseShellExecute = false
			};

			Process.Start(psi);
			AnsiConsole.MarkupLine($"[green]Launched in {terminal}:[/] {Path.GetFileName(exePath)}");
			return true;
		}
		catch (Exception ex)
		{
			AnsiConsole.MarkupLine($"[red]Failed to launch in {terminal}:[/] {ex.Message}");
			return false;
		}
	}

	private static string BuildLaunchArgs(string terminal, SchostConfig config, string exePath)
	{
		var title = config.GetEffectiveTitle();
		var cols = config.Columns;
		var rows = config.Rows;
		var font = config.Font;
		var fontSize = config.FontSize;

		return terminal switch
		{
			"ghostty" => BuildGhosttyArgs(title, cols, rows, font, fontSize, exePath),
			"kitty" => BuildKittyArgs(title, cols, rows, font, fontSize, exePath),
			"alacritty" => BuildAlacrittyArgs(title, cols, rows, exePath),
			"wezterm" => BuildWeztermArgs(title, cols, rows, font, fontSize, exePath),
			"foot" => BuildFootArgs(title, cols, rows, font, fontSize, exePath),
			"gnome-terminal" => BuildGnomeTerminalArgs(title, cols, rows, exePath),
			"konsole" => BuildKonsoleArgs(title, exePath),
			"xfce4-terminal" => BuildXfce4Args(title, cols, rows, font, fontSize, exePath),
			"xterm" => BuildXtermArgs(title, cols, rows, font, fontSize, exePath),

			// The default-terminal launchers take the command after a
			// separator and offer no geometry; reaching one means the config
			// asked for none.
			"xdg-terminal-exec" => $"-- \"{exePath}\"",
			_ => $"-e \"{exePath}\""
		};
	}

	private static string BuildGhosttyArgs(string title, int? cols, int? rows,
		string? font, int? fontSize, string exePath)
	{
		var parts = new List<string>();
		parts.Add($"--title=\"{title}\"");
		if (cols.HasValue && rows.HasValue)
			parts.Add($"--window-width={cols} --window-height={rows}");
		if (font is not null)
			parts.Add($"--font-family=\"{font}\"");
		if (fontSize.HasValue)
			parts.Add($"--font-size={fontSize}");
		parts.Add($"-e \"{exePath}\"");
		return string.Join(' ', parts);
	}

	private static string BuildKittyArgs(string title, int? cols, int? rows,
		string? font, int? fontSize, string exePath)
	{
		var parts = new List<string>();
		parts.Add($"--title=\"{title}\"");
		if (font is not null)
			parts.Add($"--override font_family=\"{font}\"");
		if (fontSize.HasValue)
			parts.Add($"--override font_size={fontSize}");
		if (cols.HasValue && rows.HasValue)
			parts.Add($"--override initial_window_width={cols}c --override initial_window_height={rows}c");
		parts.Add($"-- \"{exePath}\"");
		return string.Join(' ', parts);
	}

	private static string BuildAlacrittyArgs(string title, int? cols, int? rows, string exePath)
	{
		var parts = new List<string>();
		parts.Add($"--title \"{title}\"");
		if (cols.HasValue && rows.HasValue)
			parts.Add($"--option \"window.dimensions.columns={cols}\" --option \"window.dimensions.lines={rows}\"");
		parts.Add($"-e \"{exePath}\"");
		return string.Join(' ', parts);
	}

	private static string BuildWeztermArgs(string title, int? cols, int? rows,
		string? font, int? fontSize, string exePath)
	{
		var parts = new List<string> { "start" };
		if (cols.HasValue && rows.HasValue)
			parts.Add($"--width {cols} --height {rows}");
		parts.Add($"-- \"{exePath}\"");
		return string.Join(' ', parts);
	}

	private static string BuildFootArgs(string title, int? cols, int? rows,
		string? font, int? fontSize, string exePath)
	{
		var parts = new List<string>();
		parts.Add($"--title=\"{title}\"");
		if (cols.HasValue && rows.HasValue)
			parts.Add($"--window-size-chars={cols}x{rows}");
		if (font is not null && fontSize.HasValue)
			parts.Add($"--font=\"{font}:size={fontSize}\"");
		else if (font is not null)
			parts.Add($"--font=\"{font}\"");
		parts.Add($"\"{exePath}\"");
		return string.Join(' ', parts);
	}

	private static string BuildGnomeTerminalArgs(string title, int? cols, int? rows, string exePath)
	{
		var parts = new List<string>();
		parts.Add($"--title=\"{title}\"");
		if (cols.HasValue && rows.HasValue)
			parts.Add($"--geometry={cols}x{rows}");
		parts.Add($"-- \"{exePath}\"");
		return string.Join(' ', parts);
	}

	private static string BuildKonsoleArgs(string title, string exePath)
	{
		return $"--title \"{title}\" -e \"{exePath}\"";
	}

	private static string BuildXfce4Args(string title, int? cols, int? rows,
		string? font, int? fontSize, string exePath)
	{
		var parts = new List<string>();
		parts.Add($"--title=\"{title}\"");
		if (cols.HasValue && rows.HasValue)
			parts.Add($"--geometry={cols}x{rows}");
		if (font is not null && fontSize.HasValue)
			parts.Add($"--font=\"{font} {fontSize}\"");
		parts.Add($"-e \"{exePath}\"");
		return string.Join(' ', parts);
	}

	private static string BuildXtermArgs(string title, int? cols, int? rows,
		string? font, int? fontSize, string exePath)
	{
		var parts = new List<string>();
		parts.Add($"-title \"{title}\"");
		if (cols.HasValue && rows.HasValue)
			parts.Add($"-geometry {cols}x{rows}");
		if (font is not null)
			parts.Add($"-fa \"{font}\"");
		if (fontSize.HasValue)
			parts.Add($"-fs {fontSize}");
		parts.Add($"-e \"{exePath}\"");
		return string.Join(' ', parts);
	}

	/// <summary>
	/// Creates a .desktop file for Linux application launchers.
	/// </summary>
	public static string CreateDesktopFile(SchostConfig config, string exePath, string outputDir)
	{
		var title = config.GetEffectiveTitle();
		var assemblyName = config.AssemblyName ?? "app";
		// PickTerminal, not DetectTerminal: this file is written now and run
		// later, so whether a display exists at packaging time says nothing
		// about the machine that will launch it.
		var terminal = PickTerminal(config) ?? "xterm";
		var desktopPath = Path.Combine(outputDir, $"{assemblyName}.desktop");

		var terminalCmd = BuildLaunchArgs(terminal, config, exePath);

		var desktop = $"""
            [Desktop Entry]
            Type=Application
            Name={title}
            Exec={terminal} {terminalCmd}
            Terminal=false
            Categories=Utility;
            """;

		File.WriteAllText(desktopPath, desktop);

		// chmod +x
		try
		{
			var psi = new ProcessStartInfo("chmod", $"+x \"{desktopPath}\"")
			{
				UseShellExecute = false
			};
			Process.Start(psi)?.WaitForExit();
		}
		catch
		{
			// Best effort
		}

		AnsiConsole.MarkupLine($"[green]Created .desktop file:[/] {desktopPath}");
		return desktopPath;
	}

	private static bool IsCommandAvailable(string command)
	{
		try
		{
			var psi = new ProcessStartInfo("which", command)
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true
			};
			using var process = Process.Start(psi);
			process?.WaitForExit();
			return process?.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}
}
