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

namespace ConsoleTopExample.Stats;

/// <summary>
/// Windows-specific implementation of system statistics collection.
/// Uses Process API, Performance Counters, and NetworkInterface for CPU, memory, network, and process information.
/// </summary>
internal sealed class WindowsSystemStats : ISystemStatsProvider
{
    private DateTime _previousSample = DateTime.MinValue;
    private Dictionary<int, ProcessCpuInfo> _previousProcessCpu = new();
    private long _previousNetRx;
    private long _previousNetTx;
    private DateTime _previousNetSample = DateTime.MinValue;

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
        var processes = ReadTopProcesses();

        return new SystemSnapshot(cpu, memory, network, processes);
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

    private CpuSample ReadCpu()
    {
        var now = DateTime.UtcNow;

        try
        {
            // Calculate overall CPU usage from all processes
            var processes = Process.GetProcesses();
            double totalCpuTime = 0;
            double userTime = 0;
            double systemTime = 0;

            foreach (var proc in processes)
            {
                try
                {
                    totalCpuTime += proc.TotalProcessorTime.TotalMilliseconds;
                    userTime += proc.UserProcessorTime.TotalMilliseconds;
                    systemTime += proc.PrivilegedProcessorTime.TotalMilliseconds;
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

            // On first run, just store baseline
            if (_previousSample == DateTime.MinValue)
            {
                _previousSample = now;
                return new CpuSample(0, 0, 0);
            }

            // Calculate CPU percentage based on elapsed time
            var elapsed = (now - _previousSample).TotalMilliseconds;
            if (elapsed <= 0)
            {
                return new CpuSample(0, 0, 0);
            }

            var cpuCount = Environment.ProcessorCount;
            var maxCpu = elapsed * cpuCount;

            // Estimate user/system split (Windows doesn't expose this at system level easily)
            // Using rough 70/30 split as heuristic
            var user = Math.Min(100, (userTime / maxCpu) * 100);
            var system = Math.Min(100, (systemTime / maxCpu) * 100);

            _previousSample = now;

            // Windows doesn't have I/O wait - return 0
            return new CpuSample(user, system, 0);
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

            // Windows doesn't have a direct "cached" equivalent like Linux
            // Use GC info as rough estimate
            var gcInfo = GC.GetGCMemoryInfo();
            double cachedMb = gcInfo.TotalCommittedBytes / (1024.0 * 1024.0);
            double cachedPercent = totalPhysical > 0 ? Math.Min(100, (cachedMb / totalPhysical * 100)) : 0;

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
            }

            if (_previousNetSample == DateTime.MinValue)
            {
                _previousNetRx = totalRx;
                _previousNetTx = totalTx;
                _previousNetSample = now;
                return new NetworkSample(0, 0);
            }

            var seconds = Math.Max(0.1, (now - _previousNetSample).TotalSeconds);
            var rxDiff = Math.Max(0, totalRx - _previousNetRx);
            var txDiff = Math.Max(0, totalTx - _previousNetTx);

            _previousNetRx = totalRx;
            _previousNetTx = totalTx;
            _previousNetSample = now;

            var upMbps = (txDiff / seconds) / (1024 * 1024);
            var downMbps = (rxDiff / seconds) / (1024 * 1024);

            return new NetworkSample(upMbps, downMbps);
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
}
