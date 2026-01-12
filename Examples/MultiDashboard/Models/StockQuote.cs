namespace MultiDashboard.Models;

public class StockQuote
{
    public string Symbol { get; set; } = string.Empty;
    public double Price { get; set; }
    public double Change { get; set; }
    public double ChangePercent { get; set; }
}
