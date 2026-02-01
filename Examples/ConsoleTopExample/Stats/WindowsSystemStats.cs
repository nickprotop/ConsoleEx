// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ConsoleTopExample.Stats;

/// <summary>
/// Windows-specific implementation of system statistics collection.
/// Uses Process API, Performance Counters, and NetworkInterface for CPU, memory, network, and process information.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsSystemStats : ISystemStatsProvider
{
    private Dictionary<int, ProcessCpuInfo> _previousProcessCpu = new();
    private long _previousNetRx;
    private long _previousNetTx;
    private DateTime _previousNetSample = DateTime.MinValue;
    private Dictionary<string, NetCounters> _previousNetPerInterface = new();

    // CPU tracking for system-wide stats
    private TimeSpan _previousTotalCpuTime = TimeSpan.Zero;
    private DateTime _previousCpuSample = DateTime.MinValue;

    // Per-core CPU tracking using PerformanceCounter
    private PerformanceCounter[]? _perCoreCounters;
    private bool _perCoreCountersInitialized = false;

    // P/Invoke for Windows memory information
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MemoryStatusEx()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

    public SystemSnapshot ReadSnapshot()
    {
        var cpu = ReadCpu();
        var memory = ReadMemory();
        var network = ReadNetwork();
        var storage = ReadStorage();
        var processes = ReadTopProcesses();

        return new SystemSnapshot(cpu, memory, network, storage, processes);
    }

    public ProcessExtra? ReadProcessExtra(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);

            string state = "Running";
            try
            {
                // Windows doesn't expose detailed process state
                // Use Responding as a heuristic
                state = process.Responding ? "Running" : "Not Responding";
            }
            catch
            {
                state = "Unknown";
            }

            int threads = 0;
            try
            {
                threads = process.Threads.Count;
            }
            catch
            {
                // Access denied or process exited
            }

            double rssMb = 0;
            try
            {
                rssMb = process.WorkingSet64 / (1024.0 * 1024.0);
            }
            catch
            {
                // Access denied or process exited
            }

            // Windows Process doesn't directly expose IO rates like Linux /proc/{pid}/io
            // We would need to track IO counters over time, which requires PerformanceCounter
            // For simplicity, return 0 for now
            double readKb = 0;
            double writeKb = 0;

            string exePath = "";
            try
            {
                exePath = process.MainModule?.FileName ?? "";
            }
            catch
            {
                // Access denied or process exited
                exePath = "";
            }

            return new ProcessExtra(state, threads, rssMb, readKb, writeKb, exePath);
        }
        catch (ArgumentException)
        {
            // Process no longer exists
            return null;
        }
        catch
        {
            // Other errors (access denied, etc.)
            return null;
        }
    }

    private void InitializePerCoreCounters()
    {
        if (_perCoreCountersInitialized)
            return;

        _perCoreCountersInitialized = true;

        try
        {
            int coreCount = Environment.ProcessorCount;
            _perCoreCounters = new PerformanceCounter[coreCount];

            for (int i = 0; i < coreCount; i++)
            {
                _perCoreCounters[i] = new PerformanceCounter(
                    "Processor",
                    "% Processor Time",
                    i.ToString(),
                    true
                );
                // Call NextValue once to initialize the counter
                _perCoreCounters[i].NextValue();
            }
        }
        catch
        {
            // Failed to initialize - fall back to aggregate only
            _perCoreCounters = null;
        }
    }

    private CpuSample ReadCpu()
    {
        var now = DateTime.UtcNow;

        try
        {
            // Initialize per-core counters on first call
            InitializePerCoreCounters();

            // Calculate overall CPU usage by summing all process CPU times
            var processes = Process.GetProcesses();
            TimeSpan currentTotalCpu = TimeSpan.Zero;
            TimeSpan currentUserCpu = TimeSpan.Zero;
            TimeSpan currentSystemCpu = TimeSpan.Zero;

            foreach (var proc in processes)
            {
                try
                {
                    currentTotalCpu += proc.TotalProcessorTime;
                    currentUserCpu += proc.UserProcessorTime;
                    currentSystemCpu += proc.PrivilegedProcessorTime;
                }
                catch
                {
                    // Process may have exited or access denied - skip it
                }
                finally
                {
                    proc.Dispose();
                }
            }

            // On first run, just store baseline and return 0
            if (_previousCpuSample == DateTime.MinValue)
            {
                _previousTotalCpuTime = currentTotalCpu;
                _previousCpuSample = now;
                return new CpuSample(0, 0, 0);
            }

            // Calculate elapsed time in seconds
            var elapsedTime = (now - _previousCpuSample).TotalSeconds;
            if (elapsedTime <= 0)
            {
                return new CpuSample(0, 0, 0);
            }

            // Calculate CPU time deltas
            var deltaTotalCpu = (currentTotalCpu - _previousTotalCpuTime).TotalSeconds;
            var cpuCount = Environment.ProcessorCount;

            // CPU usage percentage = (CPU time used / elapsed real time) / number of cores * 100
            // This gives us percentage of total available CPU
            var totalCpuPercent = Math.Min(100, Math.Max(0, (deltaTotalCpu / (elapsedTime * cpuCount)) * 100));

            // Estimate user/system split based on the ratio
            // Windows aggregates these across all processes, so this is an approximation
            var userRatio = currentUserCpu.TotalSeconds / (currentUserCpu.TotalSeconds + currentSystemCpu.TotalSeconds + 0.001);
            var systemRatio = currentSystemCpu.TotalSeconds / (currentUserCpu.TotalSeconds + currentSystemCpu.TotalSeconds + 0.001);

            var userPercent = totalCpuPercent * userRatio;
            var systemPercent = totalCpuPercent * systemRatio;

            // Update for next sample
            _previousTotalCpuTime = currentTotalCpu;
            _previousCpuSample = now;

            // Read per-core CPU data if available
            List<CoreCpuSample>? perCoreSamples = null;
            if (_perCoreCounters != null)
            {
                perCoreSamples = new List<CoreCpuSample>();
                for (int i = 0; i < _perCoreCounters.Length; i++)
                {
                    try
                    {
                        float totalPct = _perCoreCounters[i].NextValue();

                        // Windows doesn't provide User/System split per-core easily
                        // Use ratio from aggregate to estimate
                        double coreUserPct = totalPct * userRatio;
                        double coreSystemPct = totalPct * systemRatio;

                        perCoreSamples.Add(new CoreCpuSample(i, coreUserPct, coreSystemPct, 0.0));
                    }
                    catch
                    {
                        // Counter may have failed - skip this core
                    }
                }
            }

            // Windows doesn't have I/O wait as a separate metric - return 0
            return new CpuSample(userPercent, systemPercent, 0, perCoreSamples);
        }
        catch
        {
            return new CpuSample(0, 0, 0);
        }
    }

    private MemorySample ReadMemory()
    {
        try
        {
            // Use P/Invoke to get Windows memory information
            var memStatus = new MemoryStatusEx();
            if (!GlobalMemoryStatusEx(memStatus))
            {
                return new MemorySample(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            }

            double totalPhysical = memStatus.ullTotalPhys / (1024.0 * 1024.0); // bytes to MB
            double availablePhysical = memStatus.ullAvailPhys / (1024.0 * 1024.0);
            double usedPhysical = totalPhysical - availablePhysical;

            double totalPageFile = memStatus.ullTotalPageFile / (1024.0 * 1024.0);
            double availablePageFile = memStatus.ullAvailPageFile / (1024.0 * 1024.0);

            // Calculate percentages
            double usedPercent = totalPhysical > 0 ? (usedPhysical / totalPhysical * 100) : 0;

            // Windows doesn't expose system cache memory like Linux /proc/meminfo
            // The "Standby List" serves as cache but isn't easily accessible via standard APIs
            // Return 0 for cache-related metrics on Windows
            double cachedMb = 0;
            double cachedPercent = 0;

            // Map Windows page file to swap
            double swapTotalMb = Math.Max(0, totalPageFile - totalPhysical);
            double swapUsedMb = Math.Max(0, (totalPageFile - availablePageFile) - usedPhysical);
            double swapFreeMb = Math.Max(0, swapTotalMb - swapUsedMb);

            // Windows doesn't expose buffers/dirty like Linux - return 0
            double buffersMb = 0;
            double dirtyMb = 0;

            return new MemorySample(
                usedPercent,
                cachedPercent,
                totalPhysical,
                usedPhysical,
                availablePhysical,
                cachedMb,
                swapTotalMb,
                swapUsedMb,
                swapFreeMb,
                buffersMb,
                dirtyMb);
        }
        catch
        {
            return new MemorySample(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }
    }

    private NetworkSample ReadNetwork()
    {
        try
        {
            var now = DateTime.UtcNow;
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            long totalRx = 0;
            long totalTx = 0;
            var currentPerInterface = new Dictionary<string, NetCounters>();

            foreach (var iface in interfaces)
            {
                // Skip loopback and non-operational interfaces
                if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    iface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                var stats = iface.GetIPv4Statistics();
                totalRx += stats.BytesReceived;
                totalTx += stats.BytesSent;

                // Track per-interface counters
                currentPerInterface[iface.Name] = new NetCounters(stats.BytesReceived, stats.BytesSent);
            }

            if (_previousNetSample == DateTime.MinValue)
            {
                _previousNetRx = totalRx;
                _previousNetTx = totalTx;
                _previousNetSample = now;
                _previousNetPerInterface = currentPerInterface;
                // Return zero rates but include interface names for UI initialization
                var initialSamples = currentPerInterface.Keys
                    .Select(name => new NetworkInterfaceSample(name, 0, 0))
                    .OrderBy(s => s.InterfaceName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return new NetworkSample(0, 0, initialSamples);
            }

            var seconds = Math.Max(0.1, (now - _previousNetSample).TotalSeconds);
            var rxDiff = Math.Max(0, totalRx - _previousNetRx);
            var txDiff = Math.Max(0, totalTx - _previousNetTx);

            // Calculate per-interface rates
            var perInterfaceSamples = new List<NetworkInterfaceSample>();
            foreach (var kvp in currentPerInterface)
            {
                if (_previousNetPerInterface.TryGetValue(kvp.Key, out var prev))
                {
                    var ifaceRxDiff = Math.Max(0, kvp.Value.RxBytes - prev.RxBytes);
                    var ifaceTxDiff = Math.Max(0, kvp.Value.TxBytes - prev.TxBytes);

                    var ifaceUpMbps = (ifaceTxDiff / seconds) / (1024 * 1024);
                    var ifaceDownMbps = (ifaceRxDiff / seconds) / (1024 * 1024);

                    perInterfaceSamples.Add(new NetworkInterfaceSample(kvp.Key, ifaceUpMbps, ifaceDownMbps));
                }
            }

            _previousNetRx = totalRx;
            _previousNetTx = totalTx;
            _previousNetSample = now;
            _previousNetPerInterface = currentPerInterface;

            var upMbps = (txDiff / seconds) / (1024 * 1024);
            var downMbps = (rxDiff / seconds) / (1024 * 1024);

            // Sort interfaces by name for consistent ordering
            perInterfaceSamples.Sort((a, b) => string.Compare(a.InterfaceName, b.InterfaceName, StringComparison.OrdinalIgnoreCase));

            return new NetworkSample(upMbps, downMbps, perInterfaceSamples);
        }
        catch
        {
            return new NetworkSample(0, 0);
        }
    }

    private List<ProcessSample> ReadTopProcesses()
    {
        try
        {
            var now = DateTime.UtcNow;
            var processes = Process.GetProcesses();
            var result = new List<ProcessSample>();

            // Get total physical memory for percentage calculation
            var memStatus = new MemoryStatusEx();
            double totalMemoryBytes = 1; // Avoid division by zero
            if (GlobalMemoryStatusEx(memStatus))
            {
                totalMemoryBytes = (double)memStatus.ullTotalPhys;
            }

            foreach (var proc in processes)
            {
                try
                {
                    int pid = proc.Id;
                    string command = proc.ProcessName;

                    // Calculate CPU percentage using delta
                    double cpuPercent = 0;
                    if (_previousProcessCpu.TryGetValue(pid, out var prevInfo))
                    {
                        var cpuDelta = (proc.TotalProcessorTime - prevInfo.TotalProcessorTime).TotalMilliseconds;
                        var timeDelta = (now - prevInfo.SampleTime).TotalMilliseconds;

                        if (timeDelta > 0)
                        {
                            // CPU % = (CPU time delta / elapsed time) * 100 / processor count
                            cpuPercent = (cpuDelta / timeDelta) * 100 / Environment.ProcessorCount;
                            cpuPercent = Math.Min(100, Math.Max(0, cpuPercent));
                        }
                    }

                    // Update CPU info for next sample
                    _previousProcessCpu[pid] = new ProcessCpuInfo(proc.TotalProcessorTime, now);

                    // Calculate memory percentage
                    double memPercent = 0;
                    try
                    {
                        memPercent = (proc.WorkingSet64 / totalMemoryBytes) * 100;
                    }
                    catch
                    {
                        // Access denied or process exited
                    }

                    result.Add(new ProcessSample(pid, cpuPercent, memPercent, command));
                }
                catch
                {
                    // Process may have exited or access denied
                }
                finally
                {
                    proc.Dispose();
                }
            }

            // Clean up stale process CPU info
            var currentPids = new HashSet<int>(result.Select(p => p.Pid));
            var stalePids = _previousProcessCpu.Keys.Where(pid => !currentPids.Contains(pid)).ToList();
            foreach (var pid in stalePids)
            {
                _previousProcessCpu.Remove(pid);
            }

            // Sort by CPU usage descending and return top processes
            return result.OrderByDescending(p => p.CpuPercent).ToList();
        }
        catch
        {
            return new List<ProcessSample>();
        }
    }

    private record ProcessCpuInfo(TimeSpan TotalProcessorTime, DateTime SampleTime);

    private StorageSample ReadStorage()
    {
        var disks = new List<DiskSample>();
        double totalCapacity = 0;
        double totalUsed = 0;
        double totalFree = 0;
        double totalRead = 0;
        double totalWrite = 0;

        try
        {
            var drives = DriveInfo.GetDrives();

            foreach (var drive in drives)
            {
                try
                {
                    // Skip drives that aren't ready (e.g., empty CD-ROM)
                    if (!drive.IsReady)
                        continue;

                    // Filter out non-physical and non-removable drives
                    // We want: Fixed (internal drives) and Removable (USB drives)
                    if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable)
                        continue;

                    double totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                    double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    double usedGb = totalGb - freeGb;
                    double usedPercent = totalGb > 0 ? (usedGb / totalGb * 100) : 0;

                    // Get I/O rates (Windows implementation)
                    var (readMbps, writeMbps) = GetDiskIoRates(drive.Name);

                    // Get volume label
                    string? label = null;
                    try
                    {
                        label = string.IsNullOrEmpty(drive.VolumeLabel) ? null : drive.VolumeLabel;
                    }
                    catch { }

                    // Get filesystem type
                    string fsType = "Unknown";
                    try
                    {
                        fsType = drive.DriveFormat;
                    }
                    catch { }

                    bool isRemovable = drive.DriveType == DriveType.Removable;

                    var disk = new DiskSample(
                        MountPoint: drive.Name,          // "C:\", "D:\", etc.
                        DeviceName: drive.Name,          // Same as mount point on Windows
                        FileSystemType: fsType,
                        Label: label,
                        MountOptions: null,              // Not applicable on Windows
                        TotalGb: totalGb,
                        UsedGb: usedGb,
                        FreeGb: freeGb,
                        UsedPercent: usedPercent,
                        ReadMbps: readMbps,
                        WriteMbps: writeMbps,
                        IsRemovable: isRemovable
                    );

                    disks.Add(disk);

                    totalCapacity += totalGb;
                    totalUsed += usedGb;
                    totalFree += freeGb;
                    totalRead += readMbps;
                    totalWrite += writeMbps;
                }
                catch
                {
                    // Skip this drive if any error occurs
                    continue;
                }
            }
        }
        catch
        {
            // If any error occurs, return empty storage data
        }

        double totalPercent = totalCapacity > 0 ? (totalUsed / totalCapacity * 100) : 0;

        return new StorageSample(
            TotalCapacityGb: totalCapacity,
            TotalUsedGb: totalUsed,
            TotalFreeGb: totalFree,
            TotalUsedPercent: totalPercent,
            TotalReadMbps: totalRead,
            TotalWriteMbps: totalWrite,
            Disks: disks
        );
    }

    private (double readMbps, double writeMbps) GetDiskIoRates(string driveName)
    {
        // For Windows, getting real-time I/O rates requires PerformanceCounter
        // which needs elevated privileges and is complex to set up correctly.
        // For now, return 0 to avoid permission issues.
        // A full implementation would use:
        // - PerformanceCounter for "PhysicalDisk" or "LogicalDisk"
        // - Counters: "Disk Read Bytes/sec" and "Disk Write Bytes/sec"
        // This would require tracking previous samples and calculating deltas.
        return (0, 0);
    }
}
