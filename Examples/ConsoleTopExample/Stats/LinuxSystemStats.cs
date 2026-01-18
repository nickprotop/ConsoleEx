// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Globalization;

namespace ConsoleTopExample.Stats;

/// <summary>
/// Linux-specific implementation of system statistics collection.
/// Reads from /proc filesystem and ps command for CPU, memory, network, and process information.
/// </summary>
internal sealed class LinuxSystemStats : ISystemStatsProvider
{
    private CpuTimes? _previousCpu;
    private Dictionary<int, CpuTimes> _previousPerCoreCpu = new();
    private Dictionary<string, NetCounters>? _previousNet;
    private DateTime _previousNetSample = DateTime.MinValue;

    public SystemSnapshot ReadSnapshot()
    {
        var cpu = ReadCpu();
        var mem = ReadMemory();
        var net = ReadNetwork();
        var procs = ReadTopProcesses();
        return new SystemSnapshot(cpu, mem, net, procs);
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
            return new NetworkSample(0, 0);
        }

        var seconds = Math.Max(0.1, (now - _previousNetSample).TotalSeconds);
        double rxDiff = 0;
        double txDiff = 0;

        foreach (var kvp in current)
        {
            if (_previousNet.TryGetValue(kvp.Key, out var prev))
            {
                rxDiff += Math.Max(0, kvp.Value.RxBytes - prev.RxBytes);
                txDiff += Math.Max(0, kvp.Value.TxBytes - prev.TxBytes);
            }
        }

        _previousNet = current;
        _previousNetSample = now;

        var upMbps = (txDiff / seconds) / (1024 * 1024);
        var downMbps = (rxDiff / seconds) / (1024 * 1024);
        return new NetworkSample(upMbps, downMbps);
    }

    private List<ProcessSample> ReadTopProcesses()
    {
        try
        {
            var psPath = File.Exists("/bin/ps") ? "/bin/ps" : "ps";
            var startInfo = new ProcessStartInfo
            {
                FileName = psPath,
                Arguments = "-eo pid,pcpu,pmem,comm --sort=-pcpu",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(startInfo);
            if (proc == null) return new List<ProcessSample>();

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(500);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1);
            var result = new List<ProcessSample>();

            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;

                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)) continue;
                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var cpu)) cpu = 0;
                if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var mem)) mem = 0;
                var cmd = string.Join(' ', parts.Skip(3));

                result.Add(new ProcessSample(pid, cpu, mem, cmd));
            }

            return result;
        }
        catch
        {
            return new List<ProcessSample>();
        }
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
}
