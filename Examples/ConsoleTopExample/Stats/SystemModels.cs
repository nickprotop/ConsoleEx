// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleTopExample.Stats;

/// <summary>
/// Snapshot of CPU usage statistics
/// </summary>
internal record CpuSample(double User, double System, double IoWait);

/// <summary>
/// Snapshot of memory usage statistics
/// </summary>
internal record MemorySample(
    double UsedPercent,
    double CachedPercent,
    double TotalMb,
    double UsedMb,
    double AvailableMb,
    double CachedMb,
    double SwapTotalMb,
    double SwapUsedMb,
    double SwapFreeMb,
    double BuffersMb,
    double DirtyMb);

/// <summary>
/// Snapshot of network interface statistics
/// </summary>
internal record NetworkSample(double UpMbps, double DownMbps);

/// <summary>
/// Information about a running process
/// </summary>
internal record ProcessSample(int Pid, double CpuPercent, double MemPercent, string Command);

/// <summary>
/// Complete snapshot of all system statistics
/// </summary>
internal record SystemSnapshot(
    CpuSample Cpu,
    MemorySample Memory,
    NetworkSample Network,
    IReadOnlyList<ProcessSample> Processes);

/// <summary>
/// Network interface counters for calculating delta
/// </summary>
internal record NetCounters(long RxBytes, long TxBytes);

/// <summary>
/// Detailed information about a specific process
/// </summary>
internal record ProcessExtra(
    string State,
    int Threads,
    double RssMb,
    double ReadKb,
    double WriteKb,
    string ExePath);
