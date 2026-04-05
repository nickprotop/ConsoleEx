// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Globalization;
using System.Runtime.InteropServices;

namespace cxtop.Stats;

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
        var loadAvg = ReadLoadAverage();
        return new SystemSnapshot(cpu, mem, net, storage, procs, loadAvg);
    }

    private static LoadAverage? ReadLoadAverage()
    {
        try
        {
            if (File.Exists("/proc/loadavg"))
            {
                var text = File.ReadAllText("/proc/loadavg");
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var l1);
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var l5);
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var l15);
                    return new LoadAverage(l1, l5, l15);
                }
            }
        }
        catch { }
        return null;
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

    public SystemInfo ReadSystemInfo()
    {
        string hostname = Environment.MachineName;
        string osDescription = GetLinuxOsDescription();
        string kernelVersion = GetLinuxKernelVersion();
        string cpuModelName = GetLinuxCpuModelName();
        string cpuArchitecture = RuntimeInformation.OSArchitecture.ToString();
        int logicalCoreCount = Environment.ProcessorCount;
        string dotNetRuntime = RuntimeInformation.FrameworkDescription;
        string motherboard = ReadDmiField("/sys/devices/virtual/dmi/id/board_name");
        string bios = ReadDmiField("/sys/devices/virtual/dmi/id/bios_version");
        string gpu = GetLinuxGpuName();
        string vendor = ReadDmiField("/sys/devices/virtual/dmi/id/sys_vendor");
        string shell = Path.GetFileName(Environment.GetEnvironmentVariable("SHELL") ?? "unknown");
        double totalRamGb = GetTotalRamGb();
        string battery = GetLinuxBatteryStatus();
        string audio = GetLinuxAudioDevice();
        int usbCount = GetLinuxUsbDeviceCount();
        string display = GetLinuxDisplayOutput();
        string resolution = GetLinuxResolution();
        string de = GetEnvOrEmpty("XDG_CURRENT_DESKTOP");
        string wm = GetLinuxWindowManager();
        string theme = GetLinuxGtkTheme();
        string icons = GetLinuxIconTheme();
        string terminal = GetLinuxTerminal();
        string termFont = ""; // requires terminal-specific queries, skip
        string locale = Environment.GetEnvironmentVariable("LANG") ?? "";
        var (pkgCount, pkgManagers) = GetLinuxPackageCounts();

        return new SystemInfo(hostname, osDescription, kernelVersion, cpuModelName,
            cpuArchitecture, logicalCoreCount, dotNetRuntime, motherboard, bios, gpu,
            vendor, shell, totalRamGb, battery, audio, usbCount, display,
            resolution, de, wm, theme, icons, terminal, termFont, locale,
            pkgCount, pkgManagers);
    }

    private static string GetEnvOrEmpty(string name)
        => Environment.GetEnvironmentVariable(name) ?? "";

    private static string GetLinuxResolution()
    {
        try
        {
            if (!Directory.Exists("/sys/class/drm")) return "";
            var resolutions = new List<string>();
            foreach (var dir in Directory.GetDirectories("/sys/class/drm"))
            {
                var statusPath = Path.Combine(dir, "status");
                if (!File.Exists(statusPath)) continue;
                if (File.ReadAllText(statusPath).Trim() != "connected") continue;

                var modesPath = Path.Combine(dir, "modes");
                if (!File.Exists(modesPath)) continue;
                // First line is the preferred/current mode
                using var reader = File.OpenText(modesPath);
                var firstMode = reader.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(firstMode))
                    resolutions.Add(firstMode);
            }
            return resolutions.Count > 0 ? string.Join(", ", resolutions) : "";
        }
        catch { }
        return "";
    }

    private static string GetLinuxWindowManager()
    {
        // XDG_SESSION_TYPE tells us wayland/x11, then we can check specific WM vars
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "";
        var wmName = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") != null
            ? "Wayland"
            : Environment.GetEnvironmentVariable("DISPLAY") != null ? "X11" : "";

        // Try to get specific WM name
        var gdmSession = Environment.GetEnvironmentVariable("GDMSESSION") ?? "";
        if (!string.IsNullOrEmpty(gdmSession))
            return $"{gdmSession} ({sessionType})".Trim();

        if (!string.IsNullOrEmpty(sessionType))
            return sessionType;

        return wmName;
    }

    private static string GetLinuxGtkTheme()
    {
        try
        {
            // Try GTK3 settings
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config/gtk-3.0/settings.ini");
            if (File.Exists(settingsPath))
            {
                foreach (var line in File.ReadLines(settingsPath))
                {
                    if (line.StartsWith("gtk-theme-name="))
                        return line.Substring(15).Trim();
                }
            }

            // Try dconf/gsettings path
            var dconfPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config/dconf/user");
            // dconf is binary, skip — GTK settings.ini is the reliable text source
        }
        catch { }
        return "";
    }

    private static string GetLinuxIconTheme()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config/gtk-3.0/settings.ini");
            if (File.Exists(settingsPath))
            {
                foreach (var line in File.ReadLines(settingsPath))
                {
                    if (line.StartsWith("gtk-icon-theme-name="))
                        return line.Substring(20).Trim();
                }
            }
        }
        catch { }
        return "";
    }

    private static string GetLinuxTerminal()
    {
        // Check common terminal env vars
        var term = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        if (!string.IsNullOrEmpty(term)) return term;

        term = Environment.GetEnvironmentVariable("TERMINAL");
        if (!string.IsNullOrEmpty(term)) return Path.GetFileName(term);

        // Fallback: walk up parent processes to find terminal
        // Or just use TERM which gives terminal type (xterm-256color etc.)
        term = Environment.GetEnvironmentVariable("TERM") ?? "";
        return term;
    }

    private static (int count, string managers) GetLinuxPackageCounts()
    {
        var parts = new List<string>();
        int total = 0;

        // dpkg (Debian/Ubuntu)
        try
        {
            var dpkgDir = "/var/lib/dpkg/info";
            if (Directory.Exists(dpkgDir))
            {
                var count = Directory.GetFiles(dpkgDir, "*.list").Length;
                if (count > 0) { total += count; parts.Add($"{count} (dpkg)"); }
            }
        }
        catch { }

        // rpm (Fedora/RHEL)
        try
        {
            var rpmDb = "/var/lib/rpm";
            if (Directory.Exists(rpmDb))
            {
                // Count entries in the rpm database Packages file
                var pkgFile = Path.Combine(rpmDb, "rpmdb.sqlite");
                if (!File.Exists(pkgFile))
                    pkgFile = Path.Combine(rpmDb, "Packages");
                if (File.Exists(pkgFile))
                {
                    // Rough estimate — can't easily parse without librpm
                    // Skip rpm if dpkg was found (avoid double-counting on some systems)
                    if (parts.Count == 0)
                        parts.Add("(rpm)");
                }
            }
        }
        catch { }

        // flatpak
        try
        {
            var flatpakDir = "/var/lib/flatpak/app";
            if (Directory.Exists(flatpakDir))
            {
                var count = Directory.GetDirectories(flatpakDir).Length;
                if (count > 0) { total += count; parts.Add($"{count} (flatpak)"); }
            }
        }
        catch { }

        // snap
        try
        {
            var snapDir = "/snap";
            if (Directory.Exists(snapDir))
            {
                // Each snap has a directory, exclude "bin" and "core*" meta dirs
                var count = Directory.GetDirectories(snapDir)
                    .Count(d => !Path.GetFileName(d).StartsWith("bin") &&
                                !Path.GetFileName(d).StartsWith("core") &&
                                !Path.GetFileName(d).StartsWith("bare") &&
                                !Path.GetFileName(d).StartsWith("snapd"));
                if (count > 0) { total += count; parts.Add($"{count} (snap)"); }
            }
        }
        catch { }

        return (total, string.Join(", ", parts));
    }

    private static string GetLinuxBatteryStatus()
    {
        try
        {
            // Try BAT0, BAT1, etc.
            foreach (var dir in Directory.GetDirectories("/sys/class/power_supply/"))
            {
                var type = Path.Combine(dir, "type");
                if (!File.Exists(type)) continue;
                if (File.ReadAllText(type).Trim() != "Battery") continue;

                var capacityPath = Path.Combine(dir, "capacity");
                var statusPath = Path.Combine(dir, "status");
                if (!File.Exists(capacityPath)) continue;

                var capacity = File.ReadAllText(capacityPath).Trim();
                var status = File.Exists(statusPath) ? File.ReadAllText(statusPath).Trim() : "Unknown";
                return $"{capacity}% ({status})";
            }
        }
        catch { }
        return "";
    }

    private static string GetLinuxAudioDevice()
    {
        try
        {
            if (File.Exists("/proc/asound/cards"))
            {
                foreach (var line in File.ReadLines("/proc/asound/cards"))
                {
                    // Lines like " 0 [sofhdadsp     ]: sof-hda-dsp - sof-hda-dsp"
                    var trimmed = line.Trim();
                    if (trimmed.Length > 0 && char.IsDigit(trimmed[0]))
                    {
                        var dashIdx = trimmed.IndexOf(" - ");
                        if (dashIdx >= 0 && dashIdx + 3 < trimmed.Length)
                            return trimmed[(dashIdx + 3)..].Trim();
                    }
                }
            }
        }
        catch { }
        return "Unknown";
    }

    private static int GetLinuxUsbDeviceCount()
    {
        try
        {
            if (Directory.Exists("/sys/bus/usb/devices"))
            {
                int count = 0;
                foreach (var dir in Directory.GetDirectories("/sys/bus/usb/devices"))
                {
                    // Real USB devices have a product file; hubs/root_hubs may not
                    var productPath = Path.Combine(dir, "product");
                    if (File.Exists(productPath))
                        count++;
                }
                return count;
            }
        }
        catch { }
        return 0;
    }

    private static string GetLinuxDisplayOutput()
    {
        try
        {
            if (Directory.Exists("/sys/class/drm"))
            {
                var connected = new List<string>();
                foreach (var dir in Directory.GetDirectories("/sys/class/drm"))
                {
                    var statusPath = Path.Combine(dir, "status");
                    if (!File.Exists(statusPath)) continue;
                    var status = File.ReadAllText(statusPath).Trim();
                    if (status == "connected")
                    {
                        // e.g., card0-eDP-1 → eDP-1
                        var name = Path.GetFileName(dir);
                        var dashIdx = name.IndexOf('-');
                        if (dashIdx >= 0 && dashIdx + 1 < name.Length)
                            connected.Add(name[(dashIdx + 1)..]);
                        else
                            connected.Add(name);
                    }
                }
                if (connected.Count > 0)
                    return string.Join(", ", connected);
            }
        }
        catch { }
        return "Unknown";
    }

    private static string ReadDmiField(string path)
    {
        try
        {
            if (File.Exists(path))
                return File.ReadAllText(path).Trim();
        }
        catch { }
        return "Unknown";
    }

    private static string GetLinuxGpuName()
    {
        try
        {
            // Try label first (some drivers provide a human-readable name)
            var labelPath = "/sys/class/drm/card0/device/label";
            if (File.Exists(labelPath))
            {
                var label = File.ReadAllText(labelPath).Trim();
                if (!string.IsNullOrEmpty(label))
                    return label;
            }

            // Try to build vendor + PCI ID description
            var ueventPath = "/sys/class/drm/card0/device/uevent";
            if (File.Exists(ueventPath))
            {
                string? pciId = null;
                string? driver = null;
                foreach (var line in File.ReadLines(ueventPath))
                {
                    if (line.StartsWith("PCI_ID="))
                        pciId = line.Substring(7).Trim();
                    else if (line.StartsWith("DRIVER="))
                        driver = line.Substring(7).Trim();
                }

                if (pciId != null)
                {
                    var vendorId = pciId.Split(':')[0].ToUpperInvariant();
                    var vendorName = vendorId switch
                    {
                        "8086" => "Intel",
                        "1002" => "AMD",
                        "10DE" => "NVIDIA",
                        _ => vendorId
                    };
                    var suffix = driver != null ? $" ({driver})" : "";
                    return $"{vendorName} [{pciId}]{suffix}";
                }
            }
        }
        catch { }
        return "Unknown";
    }

    private static double GetTotalRamGb()
    {
        try
        {
            if (File.Exists("/proc/meminfo"))
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:"))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                            return kb / (1024.0 * 1024.0);
                    }
                }
            }
        }
        catch { }
        return 0;
    }

    private static string GetLinuxOsDescription()
    {
        try
        {
            if (File.Exists("/etc/os-release"))
            {
                string? name = null;
                string? version = null;
                foreach (var line in File.ReadLines("/etc/os-release"))
                {
                    if (line.StartsWith("PRETTY_NAME="))
                        return line.Substring(12).Trim('"');
                    if (line.StartsWith("NAME="))
                        name = line.Substring(5).Trim('"');
                    else if (line.StartsWith("VERSION_ID="))
                        version = line.Substring(11).Trim('"');
                }
                if (!string.IsNullOrEmpty(name))
                    return string.IsNullOrEmpty(version) ? name : $"{name} {version}";
            }
        }
        catch { }
        return RuntimeInformation.OSDescription;
    }

    private static string GetLinuxKernelVersion()
    {
        try
        {
            if (File.Exists("/proc/version"))
            {
                var text = File.ReadAllText("/proc/version");
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                    return parts[2];
            }
        }
        catch { }
        return RuntimeInformation.OSDescription;
    }

    private static string GetLinuxCpuModelName()
    {
        try
        {
            if (File.Exists("/proc/cpuinfo"))
            {
                foreach (var line in File.ReadLines("/proc/cpuinfo"))
                {
                    if (line.StartsWith("model name"))
                    {
                        var colonIndex = line.IndexOf(':');
                        if (colonIndex >= 0)
                            return line.Substring(colonIndex + 1).Trim();
                    }
                }
            }
        }
        catch { }
        return "Unknown CPU";
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
