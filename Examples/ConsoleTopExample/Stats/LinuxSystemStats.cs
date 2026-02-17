// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Globalization;

namespace ConsoleTopExample.Stats;

/// <summary>
/// Linux-specific implementation of system statistics collection.
/// Reads exclusively from /proc and /sys — no external processes required.
/// </summary>
internal sealed class LinuxSystemStats : ISystemStatsProvider
{
    private CpuTimes? _previousCpu;
    private Dictionary<int, CpuTimes> _previousPerCoreCpu = new();
    private Dictionary<string, NetCounters>? _previousNet;
    private DateTime _previousNetSample = DateTime.MinValue;
    private Dictionary<string, DiskIoCounters> _previousDiskIo = new();
    private DateTime _previousDiskSample = DateTime.MinValue;
    private Dictionary<int, long> _previousProcessCpuTicks = new();
    private DateTime _previousProcessSample = DateTime.MinValue;
    private long _totalMemKb;

    public SystemSnapshot ReadSnapshot()
    {
        var cpu = ReadCpu();
        var mem = ReadMemory();
        var net = ReadNetwork();
        var storage = ReadStorage();
        var procs = ReadTopProcesses();
        return new SystemSnapshot(cpu, mem, net, storage, procs);
    }

    public ProcessExtra? ReadProcessExtra(int pid)
    {
        double rssMb = 0;
        int threads = 0;
        string state = "?";
        double readKb = 0;
        double writeKb = 0;
        string exePath = "";

        try
        {
            var statusPath = $"/proc/{pid}/status";
            if (File.Exists(statusPath))
            {
                foreach (var line in File.ReadLines(statusPath))
                {
                    if (line.StartsWith("VmRSS:")) rssMb = ParseLongSafe(line) / 1024.0; // kB -> MB
                    else if (line.StartsWith("Threads:")) threads = (int)ParseLongSafe(line);
                    else if (line.StartsWith("State:"))
                    {
                        var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2) state = parts[1].Trim();
                    }
                }
            }

            var ioPath = $"/proc/{pid}/io";
            if (File.Exists(ioPath))
            {
                foreach (var line in File.ReadLines(ioPath))
                {
                    if (line.StartsWith("read_bytes:")) readKb = ParseLongSafe(line) / 1024.0;
                    else if (line.StartsWith("write_bytes:")) writeKb = ParseLongSafe(line) / 1024.0;
                }
            }

            var exeLink = $"/proc/{pid}/exe";
            if (File.Exists(exeLink))
            {
                try
                {
                    exePath = Path.GetFullPath(exeLink);
                }
                catch
                {
                    exePath = exeLink;
                }
            }
        }
        catch
        {
            // Processes may exit or be inaccessible; return null to indicate process not found
            return null;
        }

        return new ProcessExtra(state, threads, rssMb, readKb, writeKb, exePath);
    }

    private CpuSample ReadCpu()
    {
        var lines = File.ReadAllLines("/proc/stat");

        // Parse aggregate CPU line
        var aggLine = lines.FirstOrDefault(l => l.StartsWith("cpu "));
        if (aggLine == null) return new CpuSample(0, 0, 0);

        var parts = aggLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 8) return new CpuSample(0, 0, 0);

        long user = ParseLong(parts[1]);
        long nice = ParseLong(parts[2]);
        long system = ParseLong(parts[3]);
        long idle = ParseLong(parts[4]);
        long iowait = ParseLong(parts[5]);
        long irq = ParseLong(parts[6]);
        long softirq = ParseLong(parts[7]);
        long steal = parts.Length > 8 ? ParseLong(parts[8]) : 0;

        var current = new CpuTimes
        {
            User = user + nice,
            System = system + irq + softirq,
            IoWait = iowait,
            Idle = idle,
            Steal = steal
        };

        if (_previousCpu == null)
        {
            _previousCpu = current;
            return new CpuSample(0, 0, 0);
        }

        var deltaUser = current.User - _previousCpu.User;
        var deltaSystem = current.System - _previousCpu.System;
        var deltaIo = current.IoWait - _previousCpu.IoWait;
        var deltaIdle = current.Idle - _previousCpu.Idle;
        var deltaSteal = current.Steal - _previousCpu.Steal;

        double total = deltaUser + deltaSystem + deltaIo + deltaIdle + deltaSteal;
        if (total <= 0)
        {
            _previousCpu = current;
            return new CpuSample(0, 0, 0);
        }

        double aggUserPct = Percent(deltaUser, total);
        double aggSystemPct = Percent(deltaSystem, total);
        double aggIoPct = Percent(deltaIo, total);

        // Parse per-core CPU lines (cpu0, cpu1, etc.)
        var perCoreSamples = new List<CoreCpuSample>();
        var coreLines = lines.Where(l => l.Length > 3 && l.StartsWith("cpu") && char.IsDigit(l[3]));

        foreach (var coreLine in coreLines)
        {
            var coreParts = coreLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (coreParts.Length < 8) continue;

            // Extract core index from "cpu0", "cpu1", etc.
            if (!int.TryParse(coreParts[0].Substring(3), out int coreIndex))
                continue;

            long coreUser = ParseLong(coreParts[1]);
            long coreNice = ParseLong(coreParts[2]);
            long coreSystem = ParseLong(coreParts[3]);
            long coreIdle = ParseLong(coreParts[4]);
            long coreIowait = ParseLong(coreParts[5]);
            long coreIrq = ParseLong(coreParts[6]);
            long coreSoftirq = ParseLong(coreParts[7]);
            long coreSteal = coreParts.Length > 8 ? ParseLong(coreParts[8]) : 0;

            var coreCurrent = new CpuTimes
            {
                User = coreUser + coreNice,
                System = coreSystem + coreIrq + coreSoftirq,
                IoWait = coreIowait,
                Idle = coreIdle,
                Steal = coreSteal
            };

            // Check if we have previous data for this core
            if (!_previousPerCoreCpu.TryGetValue(coreIndex, out var corePrev))
            {
                _previousPerCoreCpu[coreIndex] = coreCurrent;
                continue; // Skip first sample for this core
            }

            // Calculate deltas
            var coreDeltaUser = coreCurrent.User - corePrev.User;
            var coreDeltaSystem = coreCurrent.System - corePrev.System;
            var coreDeltaIo = coreCurrent.IoWait - corePrev.IoWait;
            var coreDeltaIdle = coreCurrent.Idle - corePrev.Idle;
            var coreDeltaSteal = coreCurrent.Steal - corePrev.Steal;

            double coreTotal = coreDeltaUser + coreDeltaSystem + coreDeltaIo + coreDeltaIdle + coreDeltaSteal;
            if (coreTotal > 0)
            {
                perCoreSamples.Add(new CoreCpuSample(
                    coreIndex,
                    Percent(coreDeltaUser, coreTotal),
                    Percent(coreDeltaSystem, coreTotal),
                    Percent(coreDeltaIo, coreTotal)
                ));
            }

            _previousPerCoreCpu[coreIndex] = coreCurrent;
        }

        // Sort per-core samples by core index
        perCoreSamples.Sort((a, b) => a.CoreIndex.CompareTo(b.CoreIndex));

        _previousCpu = current;
        return new CpuSample(aggUserPct, aggSystemPct, aggIoPct, perCoreSamples);
    }

    private MemorySample ReadMemory()
    {
        double total = 0;
        double available = 0;
        double cached = 0;
        double swapTotal = 0;
        double swapFree = 0;
        double buffers = 0;
        double dirty = 0;

        foreach (var line in File.ReadLines("/proc/meminfo"))
        {
            if (line.StartsWith("MemTotal:")) total = ExtractKb(line);
            else if (line.StartsWith("MemAvailable:")) available = ExtractKb(line);
            else if (line.StartsWith("Cached:")) cached = ExtractKb(line);
            else if (line.StartsWith("SwapTotal:")) swapTotal = ExtractKb(line);
            else if (line.StartsWith("SwapFree:")) swapFree = ExtractKb(line);
            else if (line.StartsWith("Buffers:")) buffers = ExtractKb(line);
            else if (line.StartsWith("Dirty:")) dirty = ExtractKb(line);
        }

        if (total <= 0) return new MemorySample(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        _totalMemKb = (long)total;

        var used = Math.Max(0, total - available);
        var usedPercent = Percent(used, total);
        var cachedPercent = Percent(cached, total);
        var swapUsed = Math.Max(0, swapTotal - swapFree);

        double totalMb = total / 1024.0;
        double usedMb = used / 1024.0;
        double availMb = available / 1024.0;
        double cachedMb = cached / 1024.0;
        double swapTotalMb = swapTotal / 1024.0;
        double swapUsedMb = swapUsed / 1024.0;
        double swapFreeMb = swapFree / 1024.0;
        double buffersMb = buffers / 1024.0;
        double dirtyMb = dirty / 1024.0;

        return new MemorySample(usedPercent, cachedPercent, totalMb, usedMb, availMb, cachedMb,
            swapTotalMb, swapUsedMb, swapFreeMb, buffersMb, dirtyMb);
    }

    private NetworkSample ReadNetwork()
    {
        var lines = File.ReadAllLines("/proc/net/dev");
        var now = DateTime.UtcNow;

        var current = new Dictionary<string, NetCounters>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(2))
        {
            var parts = line.Split(':');
            if (parts.Length != 2) continue;
            var name = parts[0].Trim();
            if (name == "lo") continue;

            var fields = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 16) continue;

            var rxBytes = ParseLong(fields[0]);
            var txBytes = ParseLong(fields[8]);
            current[name] = new NetCounters(rxBytes, txBytes);
        }

        if (_previousNet == null || _previousNetSample == DateTime.MinValue)
        {
            _previousNet = current;
            _previousNetSample = now;
            // Return zero rates but include interface names for UI initialization
            var initialSamples = current.Keys
                .Select(name => new NetworkInterfaceSample(name, 0, 0))
                .OrderBy(s => s.InterfaceName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new NetworkSample(0, 0, initialSamples);
        }

        var seconds = Math.Max(0.1, (now - _previousNetSample).TotalSeconds);
        double rxDiff = 0;
        double txDiff = 0;

        // Calculate per-interface rates and aggregate totals
        var perInterfaceSamples = new List<NetworkInterfaceSample>();

        foreach (var kvp in current)
        {
            if (_previousNet.TryGetValue(kvp.Key, out var prev))
            {
                var ifaceRxDiff = Math.Max(0, kvp.Value.RxBytes - prev.RxBytes);
                var ifaceTxDiff = Math.Max(0, kvp.Value.TxBytes - prev.TxBytes);

                rxDiff += ifaceRxDiff;
                txDiff += ifaceTxDiff;

                // Calculate per-interface MB/s
                var ifaceUpMbps = (ifaceTxDiff / seconds) / (1024 * 1024);
                var ifaceDownMbps = (ifaceRxDiff / seconds) / (1024 * 1024);

                perInterfaceSamples.Add(new NetworkInterfaceSample(kvp.Key, ifaceUpMbps, ifaceDownMbps));
            }
        }

        _previousNet = current;
        _previousNetSample = now;

        var upMbps = (txDiff / seconds) / (1024 * 1024);
        var downMbps = (rxDiff / seconds) / (1024 * 1024);

        // Sort interfaces by name for consistent ordering
        perInterfaceSamples.Sort((a, b) => string.Compare(a.InterfaceName, b.InterfaceName, StringComparison.OrdinalIgnoreCase));

        return new NetworkSample(upMbps, downMbps, perInterfaceSamples);
    }

    private List<ProcessSample> ReadTopProcesses()
    {
        // Linux CLK_TCK — clock ticks per second; 100 is the standard on all modern kernels.
        const double ClkTck = 100.0;

        var now = DateTime.UtcNow;
        var elapsed = _previousProcessSample == DateTime.MinValue
            ? 0.0
            : Math.Max(0.1, (now - _previousProcessSample).TotalSeconds);

        var result = new List<ProcessSample>();
        var currentTicks = new Dictionary<int, long>();

        try
        {
            foreach (var pidDir in Directory.GetDirectories("/proc"))
            {
                var dirName = Path.GetFileName(pidDir);
                if (!int.TryParse(dirName, out int pid)) continue;

                try
                {
                    // /proc/{pid}/stat: "pid (comm) state ... utime stime ..."
                    // comm may contain spaces/parens; split on last ')' to be safe.
                    var statText = File.ReadAllText($"/proc/{pid}/stat");
                    var lastParen = statText.LastIndexOf(')');
                    if (lastParen < 0) continue;

                    var afterParen = statText.Substring(lastParen + 2)
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    // Fields after ')': [0]=state [11]=utime [12]=stime
                    if (afterParen.Length < 13) continue;

                    long utime = ParseLong(afterParen[11]);
                    long stime = ParseLong(afterParen[12]);
                    long totalTicks = utime + stime;
                    currentTicks[pid] = totalTicks;

                    // /proc/{pid}/status: Name and VmRSS
                    string name = dirName;
                    long vmRssKb = 0;
                    foreach (var line in File.ReadLines($"/proc/{pid}/status"))
                    {
                        if (line.StartsWith("Name:"))
                            name = line.Substring(5).Trim();
                        else if (line.StartsWith("VmRSS:"))
                            vmRssKb = ParseLongSafe(line);
                    }

                    double cpuPct = 0;
                    if (elapsed > 0 && _previousProcessCpuTicks.TryGetValue(pid, out var prevTicks))
                    {
                        var delta = Math.Max(0, totalTicks - prevTicks);
                        cpuPct = Math.Round(delta / (elapsed * ClkTck) * 100.0, 1);
                    }

                    double memPct = _totalMemKb > 0
                        ? Math.Round(vmRssKb / (double)_totalMemKb * 100.0, 1)
                        : 0;

                    result.Add(new ProcessSample(pid, cpuPct, memPct, name));
                }
                catch
                {
                    // Process exited or inaccessible; skip.
                }
            }
        }
        catch { }

        _previousProcessCpuTicks = currentTicks;
        _previousProcessSample = now;

        result.Sort((a, b) => b.CpuPercent.CompareTo(a.CpuPercent));
        return result;
    }

    private static double ExtractKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return 0;
        return ParseLong(parts[1]);
    }

    private static long ParseLong(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static long ParseLongSafe(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (long.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
                return val;
        }
        return 0;
    }

    private static double Percent(double part, double total)
    {
        if (total <= 0) return 0;
        return Math.Round(part / total * 100.0, 1);
    }

    private sealed class CpuTimes
    {
        public long User { get; init; }
        public long System { get; init; }
        public long IoWait { get; init; }
        public long Idle { get; init; }
        public long Steal { get; init; }
    }

    private sealed record DiskIoCounters(long ReadSectors, long WriteSectors);

    private StorageSample ReadStorage()
    {
        var disks = new List<DiskSample>();
        double totalCapacity = 0;
        double totalUsed = 0;
        double totalFree = 0;
        double totalRead = 0;
        double totalWrite = 0;

        // Capture sample time once for all disks
        var now = DateTime.UtcNow;
        var elapsed = _previousDiskSample == DateTime.MinValue ? 0 : (now - _previousDiskSample).TotalSeconds;

        try
        {
            // Read /proc/mounts to get mounted filesystems
            if (!File.Exists("/proc/mounts"))
                return new StorageSample(0, 0, 0, 0, 0, 0, Array.Empty<DiskSample>());

            var mounts = File.ReadAllLines("/proc/mounts");

            foreach (var line in mounts)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;

                var device = parts[0];
                var mountPoint = parts[1];
                var fsType = parts[2];
                var options = parts[3];

                // Filter pseudo filesystems
                if (IsPseudoFilesystem(fsType, device))
                    continue;

                // Get disk space stats
                var diskInfo = GetDiskInfo(mountPoint);
                if (diskInfo == null) continue;

                // Get I/O stats
                var (readMbps, writeMbps) = GetDiskIoRates(device, elapsed);

                // Determine if removable
                bool isRemovable = IsRemovableDrive(device);

                // Get volume label
                string? label = GetVolumeLabel(device);

                var disk = new DiskSample(
                    MountPoint: mountPoint,
                    DeviceName: device,
                    FileSystemType: fsType,
                    Label: label,
                    MountOptions: options,
                    TotalGb: diskInfo.TotalGb,
                    UsedGb: diskInfo.UsedGb,
                    FreeGb: diskInfo.FreeGb,
                    UsedPercent: diskInfo.UsedPercent,
                    ReadMbps: readMbps,
                    WriteMbps: writeMbps,
                    IsRemovable: isRemovable
                );

                disks.Add(disk);

                totalCapacity += diskInfo.TotalGb;
                totalUsed += diskInfo.UsedGb;
                totalFree += diskInfo.FreeGb;
                totalRead += readMbps;
                totalWrite += writeMbps;
            }
        }
        catch
        {
            // If any error occurs, return empty storage data
        }

        double totalPercent = totalCapacity > 0 ? (totalUsed / totalCapacity * 100) : 0;

        // Update sample timestamp for next iteration
        _previousDiskSample = now;

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

    private bool IsPseudoFilesystem(string fsType, string device)
    {
        // Filter out pseudo/virtual filesystems
        var pseudoTypes = new[] {
            "proc", "sysfs", "devtmpfs", "tmpfs", "devpts",
            "securityfs", "cgroup", "cgroup2", "pstore",
            "bpf", "configfs", "debugfs", "tracefs", "fusectl",
            "hugetlbfs", "mqueue", "autofs"
        };

        if (pseudoTypes.Contains(fsType))
            return true;

        // Filter devices that don't start with /dev/
        if (!device.StartsWith("/dev/"))
            return true;

        return false;
    }

    private sealed record DiskInfo(double TotalGb, double UsedGb, double FreeGb, double UsedPercent);

    private DiskInfo? GetDiskInfo(string mountPoint)
    {
        try
        {
            var driveInfo = new DriveInfo(mountPoint);
            if (!driveInfo.IsReady)
                return null;

            double totalGb = driveInfo.TotalSize / (1024.0 * 1024.0 * 1024.0);
            double freeGb = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            double usedGb = totalGb - freeGb;
            double usedPercent = totalGb > 0 ? (usedGb / totalGb * 100) : 0;

            return new DiskInfo(totalGb, usedGb, freeGb, usedPercent);
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeBlockDevice(string device)
    {
        // Loop devices: Don't normalize - keep loop0, loop1 separate
        if (device.StartsWith("loop"))
            return device;

        // Handle NVMe devices: nvme0n1p1 -> nvme0n1p1 (keep partition number for per-partition stats)
        // Handle MMC/SD cards: mmcblk0p1 -> mmcblk0p1 (keep partition number)
        // Handle traditional disks: sda1 -> sda1 (keep partition number)

        // Actually, we should track per-partition I/O, not per-device!
        // Each partition has its own I/O stats in /proc/diskstats
        return device;
    }

    private (double readMbps, double writeMbps) GetDiskIoRates(string device, double elapsed)
    {
        try
        {
            // Extract device name (e.g., "sda" from "/dev/sda1")
            var deviceName = Path.GetFileName(device);
            if (string.IsNullOrEmpty(deviceName))
                return (0, 0);

            // Keep device name as-is - each partition has its own I/O stats in /proc/diskstats
            deviceName = NormalizeBlockDevice(deviceName);
            if (string.IsNullOrEmpty(deviceName))
                return (0, 0);

            // Read /proc/diskstats
            if (!File.Exists("/proc/diskstats"))
                return (0, 0);

            var diskStats = File.ReadAllLines("/proc/diskstats");
            foreach (var line in diskStats)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 14) continue;
                if (parts[2] != deviceName) continue;

                long readSectors = long.Parse(parts[5]);   // sectors read
                long writeSectors = long.Parse(parts[9]);  // sectors written

                if (_previousDiskIo.TryGetValue(deviceName, out var last) && elapsed > 0)
                {
                    // Calculate delta
                    long readDelta = readSectors - last.ReadSectors;
                    long writeDelta = writeSectors - last.WriteSectors;

                    // Sectors are typically 512 bytes
                    double readMbps = (readDelta * 512.0) / (1024.0 * 1024.0 * elapsed);
                    double writeMbps = (writeDelta * 512.0) / (1024.0 * 1024.0 * elapsed);

                    _previousDiskIo[deviceName] = new DiskIoCounters(readSectors, writeSectors);

                    return (Math.Max(0, readMbps), Math.Max(0, writeMbps));
                }

                // First sample - store counters, return 0 rates
                _previousDiskIo[deviceName] = new DiskIoCounters(readSectors, writeSectors);
                return (0, 0);
            }
        }
        catch { }

        return (0, 0);
    }

    private bool IsRemovableDrive(string device)
    {
        try
        {
            var deviceName = Path.GetFileName(device);
            if (string.IsNullOrEmpty(deviceName))
                return false;

            // Remove partition number
            deviceName = new string(deviceName.TakeWhile(c => !char.IsDigit(c)).ToArray());
            if (string.IsNullOrEmpty(deviceName))
                return false;

            var removablePath = $"/sys/block/{deviceName}/removable";
            if (File.Exists(removablePath))
            {
                var content = File.ReadAllText(removablePath).Trim();
                return content == "1";
            }
        }
        catch { }

        return false;
    }

    private static string? GetVolumeLabel(string device)
    {
        try
        {
            // Resolve labels from /dev/disk/by-label/ symlinks — no external process needed.
            const string labelDir = "/dev/disk/by-label";
            if (!Directory.Exists(labelDir)) return null;

            var deviceName = Path.GetFileName(device);
            foreach (var labelPath in Directory.GetFiles(labelDir))
            {
                var target = new FileInfo(labelPath).ResolveLinkTarget(returnFinalTarget: true);
                if (target != null && Path.GetFileName(target.FullName) == deviceName)
                    return Path.GetFileName(labelPath);
            }
        }
        catch { }

        return null;
    }
}
