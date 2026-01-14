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

    /// <summary>
    /// Gets detailed system information including OS name, version, kernel, architecture, and CPU count.
    /// </summary>
    /// <returns>A compact string with system information formatted for display.</returns>
    public static string GetDetailedSystemInfo()
    {
        string osName = GetOSName();
        string kernel = GetKernelVersion();
        string arch = RuntimeInformation.ProcessArchitecture.ToString();
        int cpuCount = Environment.ProcessorCount;

        return $"{osName} • {kernel} • {arch} • {cpuCount} cores";
    }

    /// <summary>
    /// Gets the OS name and version (e.g., "Ubuntu 24.04", "Windows 11", "Fedora 40").
    /// </summary>
    private static string GetOSName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsVersion();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetLinuxDistro();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetMacOSVersion();
        }

        return "Unknown OS";
    }

    /// <summary>
    /// Gets the kernel version string.
    /// </summary>
    private static string GetKernelVersion()
    {
        try
        {
            string osDesc = RuntimeInformation.OSDescription;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Extract kernel version from OSDescription (e.g., "Linux 6.17.0-8-generic")
                var parts = osDesc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    return parts[1]; // Just the version number
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Extract Windows build number (e.g., "10.0.22621")
                var parts = osDesc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (part.Contains('.') && char.IsDigit(part[0]))
                    {
                        return $"NT {part}";
                    }
                }
            }

            return osDesc;
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Gets Windows version and edition (e.g., "Windows 11 Pro", "Windows 10").
    /// </summary>
    private static string GetWindowsVersion()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var reg = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (reg != null)
                {
                    string? productName = reg.GetValue("ProductName")?.ToString();
                    if (!string.IsNullOrEmpty(productName))
                    {
                        return productName;
                    }
                }
            }
        }
        catch
        {
            // Fall through to default
        }

        return "Windows";
    }

    /// <summary>
    /// Gets Linux distribution name and version (e.g., "Ubuntu 24.04", "Fedora 40").
    /// </summary>
    private static string GetLinuxDistro()
    {
        try
        {
            if (File.Exists("/etc/os-release"))
            {
                var lines = File.ReadAllLines("/etc/os-release");
                string? name = null;
                string? version = null;

                foreach (var line in lines)
                {
                    if (line.StartsWith("NAME="))
                    {
                        name = line.Substring(5).Trim('"');
                    }
                    else if (line.StartsWith("VERSION_ID="))
                    {
                        version = line.Substring(11).Trim('"');
                    }
                }

                if (!string.IsNullOrEmpty(name))
                {
                    return string.IsNullOrEmpty(version) ? name : $"{name} {version}";
                }
            }
        }
        catch
        {
            // Fall through to default
        }

        return "Linux";
    }

    /// <summary>
    /// Gets macOS version (e.g., "macOS 14.0").
    /// </summary>
    private static string GetMacOSVersion()
    {
        try
        {
            string osDesc = RuntimeInformation.OSDescription;
            // OSDescription might contain version info
            return osDesc.Contains("Darwin") ? "macOS" : osDesc;
        }
        catch
        {
            return "macOS";
        }
    }
}
