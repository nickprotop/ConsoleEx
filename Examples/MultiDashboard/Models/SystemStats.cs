namespace MultiDashboard.Models;

public class SystemStats
{
    public double CpuUser { get; set; }
    public double CpuSystem { get; set; }
    public double CpuIo { get; set; }
    public double MemoryUsed { get; set; }
    public double MemoryCached { get; set; }
    public int DiskReadMbps { get; set; }
    public int DiskWriteMbps { get; set; }
}
