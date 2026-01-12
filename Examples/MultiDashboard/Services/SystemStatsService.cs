using MultiDashboard.Models;

namespace MultiDashboard.Services;

public class SystemStatsService
{
    private readonly Random _random = new();
    private double _cpuPhase = 0;
    private double _memoryUsed = 60;

    public SystemStats GetCurrentStats()
    {
        // Sine wave for CPU (looks realistic)
        _cpuPhase += 0.1;
        var cpuUser = 30 + 25 * Math.Sin(_cpuPhase);
        var cpuSystem = 15 + 10 * Math.Sin(_cpuPhase * 0.7);
        var cpuIo = 5 + 5 * Math.Sin(_cpuPhase * 1.3);

        // Random walk for memory
        _memoryUsed += (_random.NextDouble() - 0.5) * 2;
        _memoryUsed = Math.Clamp(_memoryUsed, 40, 85);

        return new SystemStats
        {
            CpuUser = Math.Max(0, cpuUser),
            CpuSystem = Math.Max(0, cpuSystem),
            CpuIo = Math.Max(0, cpuIo),
            MemoryUsed = _memoryUsed,
            MemoryCached = _random.Next(10, 20),
            DiskReadMbps = _random.Next(50, 200),
            DiskWriteMbps = _random.Next(20, 100)
        };
    }
}
