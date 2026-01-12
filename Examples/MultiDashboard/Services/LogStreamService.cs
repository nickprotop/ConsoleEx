namespace MultiDashboard.Services;

public class LogStreamService
{
    private readonly Random _random = new();
    private readonly List<string> _logTemplates = new()
    {
        "Processing request from {0}",
        "Database query completed in {0}ms",
        "Cache hit for key: {0}",
        "User {0} authenticated successfully",
        "API call to {0} returned {1}",
        "Background job {0} started",
        "File {0} uploaded successfully",
        "Connection established to {0}",
        "Transaction {0} committed",
        "Service {0} health check passed",
        "Message queued for delivery to {0}",
        "Scheduled task {0} executed",
        "Configuration reloaded from {0}",
        "Memory usage: {0}MB",
        "Active connections: {0}"
    };

    private readonly string[] _randomWords = {
        "alpha", "beta", "gamma", "delta", "epsilon",
        "service-a", "service-b", "worker-1", "worker-2",
        "192.168.1.1", "api.example.com", "cache-01"
    };

    public string GenerateRandomLog()
    {
        var template = _logTemplates[_random.Next(_logTemplates.Count)];
        var word1 = _randomWords[_random.Next(_randomWords.Length)];
        var word2 = _randomWords[_random.Next(_randomWords.Length)];
        var number = _random.Next(1, 1000);

        try
        {
            return string.Format(template, word1, word2, number);
        }
        catch
        {
            return string.Format(template, word1);
        }
    }
}
