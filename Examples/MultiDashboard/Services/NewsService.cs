using MultiDashboard.Models;

namespace MultiDashboard.Services;

public class NewsService
{
    private readonly Random _random = new();
    private readonly List<string> _headlines = new()
    {
        "Tech Giant Announces New AI Framework for Developers",
        "Stock Market Reaches All-Time High Amid Tech Rally",
        "New Programming Language Gains Popularity Among Developers",
        "Cloud Computing Costs Expected to Drop 30% This Year",
        "Cybersecurity Threat Levels Increase Globally",
        "Major Software Update Brings Performance Improvements",
        "Industry Leaders Meet to Discuss Future of Technology",
        "New Study Shows Remote Work Productivity Gains",
        "Quantum Computing Breakthrough Announced by Researchers",
        "Mobile Device Sales Surge Despite Economic Concerns",
        "Open Source Project Reaches 100K Stars on GitHub",
        "Tech Conference Announces Record Attendance Numbers",
        "New Development Tools Released for Cross-Platform Apps",
        "Data Center Expansion Plans Revealed by Cloud Provider",
        "Software Patent Ruling Could Impact Industry Standards",
        "New Chip Architecture Promises 50% Better Performance",
        "Developer Survey Reveals Most Popular Technologies",
        "Tech Startup Raises $100M in Series B Funding",
        "Platform Update Introduces Advanced Security Features",
        "Industry Report Shows Growing Investment in AI"
    };

    private readonly string[] _sources = { "TechCrunch", "The Verge", "Ars Technica", "Wired", "Reuters", "Bloomberg" };

    public List<NewsItem> GetLatestNews()
    {
        return _headlines
            .OrderBy(_ => _random.Next())
            .Take(5)
            .Select(headline => new NewsItem
            {
                Headline = headline,
                Source = _sources[_random.Next(_sources.Length)],
                Time = DateTime.Now.AddMinutes(-_random.Next(0, 60))
            })
            .ToList();
    }
}
