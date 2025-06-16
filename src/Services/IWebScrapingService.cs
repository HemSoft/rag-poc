namespace RagPoc.Services;

public interface IWebScrapingService
{
    Task<string> ScrapeWebsiteAsync(string url);
    Task<string> ScrapeWebsiteAsync(string url, string selector);
}
