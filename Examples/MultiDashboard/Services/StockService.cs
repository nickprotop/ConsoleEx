using MultiDashboard.Models;

namespace MultiDashboard.Services;

public class StockService
{
    private readonly Dictionary<string, StockQuote> _quotes = new();
    private readonly Random _random = new();

    public StockService()
    {
        // Initialize stocks
        InitializeStocks(new[] {
            ("AAPL", 180.0),
            ("MSFT", 380.0),
            ("GOOGL", 140.0),
            ("AMZN", 170.0),
            ("TSLA", 250.0),
            ("META", 470.0),
            ("NVDA", 870.0),
            ("AMD", 160.0),
            ("INTC", 45.0),
            ("NFLX", 590.0)
        });
    }

    private void InitializeStocks((string Symbol, double Price)[] stocks)
    {
        foreach (var (symbol, price) in stocks)
        {
            _quotes[symbol] = new StockQuote
            {
                Symbol = symbol,
                Price = price,
                Change = 0,
                ChangePercent = 0
            };
        }
    }

    public List<StockQuote> GetLatestQuotes()
    {
        foreach (var symbol in _quotes.Keys)
        {
            var quote = _quotes[symbol];
            var oldPrice = quote.Price;

            // Random walk with slight upward drift
            var change = (_random.NextDouble() - 0.48) * 2;
            quote.Price = Math.Max(1, quote.Price + change);

            quote.Change = quote.Price - oldPrice;
            quote.ChangePercent = (quote.Change / oldPrice) * 100;
        }

        return _quotes.Values.OrderBy(q => q.Symbol).ToList();
    }
}
