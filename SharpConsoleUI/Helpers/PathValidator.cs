// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Centralised path validation for preventing path traversal, symlink following,
	/// and access to sensitive system directories. All file-access paths in the
	/// framework that accept external input should validate through this class.
	/// </summary>
	public static class PathValidator
	{
		/// <summary>
		/// Blocked directory prefixes on Unix systems. Paths rooted in these directories
		/// are rejected by <see cref="IsSensitiveSystemPath"/> to prevent writes to
		/// critical system locations.
		/// </summary>
		private static readonly string[] BlockedUnixPrefixes = { "/etc/", "/usr/", "/bin/", "/sbin/", "/proc/", "/sys/", "/dev/" };
		private static readonly string[] BlockedUnixExact = { "/", "/etc", "/usr", "/bin", "/sbin", "/proc", "/sys", "/dev" };

		/// <summary>
		/// Returns true if <paramref name="filePath"/> is contained within
		/// <paramref name="allowedDirectory"/> after full path canonicalization.
		/// Uses case-insensitive comparison for cross-platform correctness.
		/// </summary>
		/// <param name="filePath">The file path to validate.</param>
		/// <param name="allowedDirectory">The directory that must contain the file.</param>
		/// <returns>True if the file is within the allowed directory.</returns>
		public static bool IsPathWithinDirectory(string filePath, string allowedDirectory)
		{
			var fullFilePath = Path.GetFullPath(filePath);
			var fullDirPath = Path.GetFullPath(allowedDirectory);

			if (!fullDirPath.EndsWith(Path.DirectorySeparatorChar))
				fullDirPath += Path.DirectorySeparatorChar;

			return fullFilePath.StartsWith(fullDirPath, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Returns true if the given path points to a symlink (reparse point).
		/// </summary>
		/// <param name="path">The file or directory path to check.</param>
		/// <returns>True if the path is a symbolic link or junction.</returns>
		public static bool IsSymlink(string path)
		{
			try
			{
				if (File.Exists(path))
					return (new FileInfo(path).Attributes & FileAttributes.ReparsePoint) != 0;
				if (Directory.Exists(path))
					return (new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) != 0;
				return false;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Returns true if the path targets a sensitive system directory that
		/// should not be written to by application code. Checks common Unix
		/// system paths; on Windows, returns false (system paths are ACL-protected).
		/// </summary>
		/// <param name="path">The file or directory path to check.</param>
		/// <returns>True if the path is within a sensitive system directory.</returns>
		public static bool IsSensitiveSystemPath(string path)
		{
			if (OperatingSystem.IsWindows())
				return false;

			var normalized = Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');

			foreach (var exact in BlockedUnixExact)
			{
				if (string.Equals(normalized, exact, StringComparison.Ordinal))
					return true;
			}

			foreach (var prefix in BlockedUnixPrefixes)
			{
				if (normalized.StartsWith(prefix, StringComparison.Ordinal))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Validates that a file path is safe to access: it must be within the allowed
		/// directory, must not be a symlink, and must not target a sensitive system path.
		/// </summary>
		/// <param name="filePath">The file path to validate.</param>
		/// <param name="allowedDirectory">The directory that must contain the file.</param>
		/// <param name="reason">If validation fails, the reason why.</param>
		/// <returns>True if the path passes all validation checks.</returns>
		public static bool ValidatePath(string filePath, string allowedDirectory, out string reason)
		{
			reason = string.Empty;

			if (string.IsNullOrWhiteSpace(filePath))
			{
				reason = "Path is empty.";
				return false;
			}

			if (!IsPathWithinDirectory(filePath, allowedDirectory))
			{
				reason = "Path is outside the allowed directory.";
				return false;
			}

			if (IsSymlink(filePath))
			{
				reason = "Path is a symbolic link.";
				return false;
			}

			if (IsSensitiveSystemPath(filePath))
			{
				reason = "Path targets a sensitive system directory.";
				return false;
			}

			return true;
		}
	}
}
