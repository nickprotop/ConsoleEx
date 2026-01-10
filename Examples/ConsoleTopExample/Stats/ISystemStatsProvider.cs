// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleTopExample.Stats;

/// <summary>
/// Platform-independent interface for collecting system statistics.
/// Implementations provide platform-specific logic for reading CPU, memory,
/// network, and process information.
/// </summary>
internal interface ISystemStatsProvider
{
    /// <summary>
    /// Reads a complete snapshot of system statistics including CPU, memory,
    /// network, and process information.
    /// </summary>
    /// <returns>A complete system snapshot</returns>
    SystemSnapshot ReadSnapshot();

    /// <summary>
    /// Reads detailed information for a specific process by PID.
    /// Returns null if the process no longer exists.
    /// </summary>
    /// <param name="pid">The process ID to read details for</param>
    /// <returns>Detailed process information, or null if process not found</returns>
    ProcessExtra? ReadProcessExtra(int pid);
}
