// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace ConsoleTopExample.Stats;

/// <summary>
/// Factory for creating platform-specific system statistics providers.
/// Automatically detects the current platform and returns the appropriate implementation.
/// </summary>
internal static class SystemStatsFactory
{
    /// <summary>
    /// Creates a platform-specific system statistics provider based on the current operating system.
    /// </summary>
    /// <returns>An implementation of ISystemStatsProvider for the current platform.</returns>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when the current platform is not Windows or Linux.
    /// </exception>
    public static ISystemStatsProvider Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsSystemStats();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxSystemStats();
        }

        throw new PlatformNotSupportedException(
            $"Platform {RuntimeInformation.OSDescription} is not supported. " +
            "Supported platforms: Windows, Linux");
    }

    /// <summary>
    /// Gets a human-readable name for the current platform.
    /// </summary>
    public static string GetPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS";

        return "Unknown";
    }
}
