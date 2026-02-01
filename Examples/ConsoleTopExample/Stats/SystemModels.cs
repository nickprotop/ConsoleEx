// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleTopExample.Stats;

/// <summary>
/// Per-core CPU usage statistics
/// </summary>
internal record CoreCpuSample(int CoreIndex, double User, double System, double IoWait);

/// <summary>
/// Snapshot of CPU usage statistics
/// </summary>
internal record CpuSample(
    double User,
    double System,
    double IoWait,
    IReadOnlyList<CoreCpuSample>? PerCoreSamples = null);

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
/// Per-interface network statistics
/// </summary>
internal record NetworkInterfaceSample(
    string InterfaceName,    // "eth0", "wlan0", "Ethernet", etc.
    double UpMbps,           // Upload (TX) rate
    double DownMbps);        // Download (RX) rate

/// <summary>
/// Snapshot of network interface statistics
/// </summary>
internal record NetworkSample(
    double UpMbps,
    double DownMbps,
    IReadOnlyList<NetworkInterfaceSample>? PerInterfaceSamples = null);

/// <summary>
/// Information about a running process
/// </summary>
internal record ProcessSample(int Pid, double CpuPercent, double MemPercent, string Command);

/// <summary>
/// Storage/disk usage for a single filesystem/volume
/// </summary>
internal record DiskSample(
    string MountPoint,       // "/" or "C:\"
    string DeviceName,       // "/dev/sda1" or "C:"
    string FileSystemType,   // "ext4", "ntfs", "xfs", "btrfs", etc.
    string? Label,           // Optional volume label/name
    string? MountOptions,    // "rw,relatime" or null
    double TotalGb,          // Total capacity in GB
    double UsedGb,           // Used space in GB
    double FreeGb,           // Free space in GB
    double UsedPercent,      // Used percentage (0-100)
    double ReadMbps,         // Current read rate in MB/s
    double WriteMbps,        // Current write rate in MB/s
    bool IsRemovable);       // True for USB/external drives

/// <summary>
/// Aggregate storage statistics
/// </summary>
internal record StorageSample(
    double TotalCapacityGb,     // Sum of all disk capacities
    double TotalUsedGb,         // Sum of all used space
    double TotalFreeGb,         // Sum of all free space
    double TotalUsedPercent,    // Overall usage percentage
    double TotalReadMbps,       // Sum of all read rates
    double TotalWriteMbps,      // Sum of all write rates
    IReadOnlyList<DiskSample> Disks);

/// <summary>
/// Complete snapshot of all system statistics
/// </summary>
internal record SystemSnapshot(
    CpuSample Cpu,
    MemorySample Memory,
    NetworkSample Network,
    StorageSample Storage,
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
