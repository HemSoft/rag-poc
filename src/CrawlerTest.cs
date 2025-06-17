using RagPoc.Services;
using RagPoc.Configuration;
using Microsoft.Extensions.Options;

namespace RagPoc;

public class CrawlerTest
{
    public static async Task TestCrawler()
    {
        var httpClient = new HttpClient();
        var options = Options.Create(new WebCrawlerOptions
        {
            MaxDepth = 2,
            MaxPages = 5,
            DelayBetweenRequests = 1000,
            SameOriginOnly = true
        });

        var crawler = new WebScrapingService(httpClient, options);

        try
        {
            Console.WriteLine("Testing Abot-based crawler...");
            var result = await crawler.CrawlWebsiteAsync("https://docs.microsoft.com/en-us/dotnet/");
            
            Console.WriteLine($"Crawl completed. Content length: {result.Length}");
            Console.WriteLine("First 500 characters of crawled content:");
            Console.WriteLine(result.Substring(0, Math.Min(500, result.Length)));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
