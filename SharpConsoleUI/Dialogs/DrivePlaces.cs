// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpConsoleUI.Dialogs;

/// <summary>
/// OS-aware enumeration of drives / mount points ("places") for the file and
/// folder picker dialogs. Centralized here so the three dialogs share one
/// implementation (no duplication).
/// </summary>
internal static class DrivePlaces
{
	/// <summary>A drive as enumerated from the OS, before filtering. Test seam.</summary>
	internal readonly record struct RawDrive(
		string RootPath,
		string VolumeLabel,
		string DriveType,
		bool IsReady,
		long TotalSize,
		long AvailableFreeSpace);

	/// <summary>A place the user can jump to in the picker.</summary>
	internal readonly record struct PlaceEntry(
		string Path,
		string DisplayName,
		string Detail,
		string Icon,
		bool IsCurrent);

	// Linux virtual / pseudo filesystems and noisy mount roots to hide.
	// Mirrors the cxfiles FileSystemService blacklist.
	private static readonly string[] VirtualFsRoots =
	{
		"/proc", "/sys", "/dev", "/run", "/snap",
		"/var/lib/docker", "/var/lib/containers", "/var/lib/lxcfs"
	};

	/// <summary>Production entry point: enumerates the real drive table.</summary>
	public static IReadOnlyList<PlaceEntry> GetPlaces(string? currentPath)
		=> GetPlaces(
			currentPath,
			EnumerateRealDrives(),
			OperatingSystem.IsWindows(),
			SafeHome());

	/// <summary>Test seam: filters an injected drive list.</summary>
	internal static IReadOnlyList<PlaceEntry> GetPlaces(
		string? currentPath,
		IEnumerable<RawDrive> drives,
		bool isWindows,
		string? homePath)
	{
		var kept = new List<RawDrive>();
		var seenRoots = new HashSet<string>(StringComparer.Ordinal);
		foreach (var d in drives)
		{
			if (!d.IsReady) continue;
			if (!isWindows && !ShouldShowDrive(d)) continue;
			// The OS can report the same mount more than once (e.g. network
			// shares appear twice in DriveInfo.GetDrives()). Keep the first.
			if (!seenRoots.Add(d.RootPath)) continue;
			kept.Add(d);
		}

		string normalizedCurrent = NormalizeForMatch(currentPath);
		string? bestRoot = LongestPrefixRoot(normalizedCurrent, kept.Select(k => k.RootPath));

		var result = new List<PlaceEntry>();
		foreach (var d in kept)
		{
			result.Add(new PlaceEntry(
				Path: d.RootPath,
				DisplayName: d.RootPath,
				Detail: BuildDetail(d),
				Icon: IconFor(d.DriveType),
				IsCurrent: string.Equals(d.RootPath, bestRoot, StringComparison.Ordinal)));
		}

		// Synthetic Home entry on all platforms.
		if (!string.IsNullOrEmpty(homePath))
		{
			bool homeIsCurrent = bestRoot == null
				&& normalizedCurrent.StartsWith(NormalizeForMatch(homePath), StringComparison.Ordinal);
			result.Add(new PlaceEntry(homePath!, "Home", homePath!, "⌂", homeIsCurrent));
		}

		return result;
	}

	private static bool ShouldShowDrive(RawDrive d)
	{
		var path = d.RootPath;
		if (path == "/") return true;
		if (path == "/home" || path == "/home/") return true;
		if (IsBlacklistedVirtualFs(path)) return false;
		if (path.StartsWith("/boot", StringComparison.Ordinal)) return false;
		if (path.StartsWith("/media/", StringComparison.Ordinal)) return true;
		if (path.StartsWith("/mnt/", StringComparison.Ordinal)) return true;
		if (path == "/mnt") return true;
		if (d.DriveType == "Network") return true;
		if (d.DriveType == "Removable") return true;
		if (d.DriveType == "CDRom") return true;
		return false;
	}

	private static bool IsBlacklistedVirtualFs(string fullPath)
	{
		foreach (var v in VirtualFsRoots)
		{
			if (fullPath == v) return true;
			if (fullPath.Length > v.Length &&
				fullPath.StartsWith(v, StringComparison.Ordinal) &&
				(fullPath[v.Length] == '/' || fullPath[v.Length] == Path.DirectorySeparatorChar))
				return true;
		}
		return false;
	}

	private static string IconFor(string driveType) => driveType switch
	{
		"Network" => "🌐",
		"CDRom" => "💿",
		"Removable" => "🔌",
		_ => "💾"
	};

	private static string BuildDetail(RawDrive d)
	{
		// Ignore labels that merely repeat the mount path (common on Linux,
		// where VolumeLabel is often the root path itself).
		var hasUsefulLabel = !string.IsNullOrWhiteSpace(d.VolumeLabel)
			&& !string.Equals(d.VolumeLabel, d.RootPath, StringComparison.Ordinal);
		var label = hasUsefulLabel ? d.VolumeLabel : d.DriveType;
		if (d.TotalSize > 0)
			return $"{label} · {FormatSize(d.AvailableFreeSpace)} free";
		return label;
	}

	private static string FormatSize(long bytes) => bytes switch
	{
		< 1024 => $"{bytes} B",
		< 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
		< 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
		_ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
	};

	private static string NormalizeForMatch(string? path)
	{
		if (string.IsNullOrEmpty(path)) return "";
		try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
		catch { return path; }
	}

	private static string? LongestPrefixRoot(string normalizedCurrent, IEnumerable<string> roots)
	{
		string? best = null;
		int bestLen = -1;
		foreach (var root in roots)
		{
			var r = NormalizeForMatch(root);
			bool match = normalizedCurrent == r
				|| normalizedCurrent.StartsWith(
					r.EndsWith(Path.DirectorySeparatorChar) ? r : r + Path.DirectorySeparatorChar,
					StringComparison.Ordinal);
			if (match && r.Length > bestLen)
			{
				best = root;
				bestLen = r.Length;
			}
		}
		return best;
	}

	private static string? SafeHome()
	{
		try
		{
			var h = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			return string.IsNullOrEmpty(h) ? null : h;
		}
		catch { return null; }
	}

	private static IEnumerable<RawDrive> EnumerateRealDrives()
	{
		DriveInfo[] drives;
		try { drives = DriveInfo.GetDrives(); }
		catch { yield break; }

		foreach (var d in drives)
		{
			RawDrive raw;
			try
			{
				bool ready = d.IsReady;
				raw = new RawDrive(
					d.RootDirectory.FullName,
					ready ? SafeLabel(d) : "",
					d.DriveType.ToString(),
					ready,
					ready ? SafeTotal(d) : 0,
					ready ? SafeFree(d) : 0);
			}
			catch { continue; }
			yield return raw;
		}
	}

	private static string SafeLabel(DriveInfo d) { try { return d.VolumeLabel ?? ""; } catch { return ""; } }
	private static long SafeTotal(DriveInfo d) { try { return d.TotalSize; } catch { return 0; } }
	private static long SafeFree(DriveInfo d) { try { return d.AvailableFreeSpace; } catch { return 0; } }

	/// <summary>
	/// Splits a path into its drive/mount segment (rendered as the chip) and the
	/// remainder. Purely lexical — does not consult the live mount table.
	/// Windows: "C:\Users" -> ("C:", "\Users").
	/// Linux:   "/media/usb/p" -> ("/media/usb", "/p");  "/home/n" -> ("/", "home/n").
	/// </summary>
	internal static (string Segment, string Remainder) SplitDriveSegment(string path)
	{
		if (string.IsNullOrEmpty(path)) return ("", "");

		// Windows-style "X:" prefix.
		if (path.Length >= 2 && path[1] == ':')
			return (path.Substring(0, 2), path.Substring(2));

		if (!path.StartsWith("/", StringComparison.Ordinal))
			return ("", path);

		// Linux: treat /media/<x> and /mnt/<x> as a two-segment mount chip.
		foreach (var prefix in new[] { "/media/", "/mnt/" })
		{
			if (path.StartsWith(prefix, StringComparison.Ordinal))
			{
				int next = path.IndexOf('/', prefix.Length);
				if (next < 0) return (path, "");
				return (path.Substring(0, next), path.Substring(next));
			}
		}

		// Everything else under "/" -> root is the chip.
		return ("/", path.Substring(1));
	}

	/// <summary>
	/// Builds the markup for the clickable drive chip with the embedded
	/// "(Ctrl+D)" hint, e.g. "[black on deepskyblue1] 💾 C: (Ctrl+D) [/]".
	/// </summary>
	internal static string BuildChipMarkup(string driveSegment)
	{
		var safe = driveSegment.Replace("[", "[[").Replace("]", "]]");
		return $"[black on deepskyblue1] 💾 {safe} (Ctrl+D) [/]";
	}
}
