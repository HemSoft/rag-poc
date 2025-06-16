using AngleSharp;
using AngleSharp.Html.Dom;
using System.Text;

namespace RagPoc.Services;

public class WebScrapingService : IWebScrapingService
{
    private readonly HttpClient _httpClient;

    public WebScrapingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }    public async Task<string> ScrapeWebsiteAsync(string url)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(url);
            
            var config = AngleSharp.Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html));

            // Remove script and style elements
            var scriptsAndStyles = document.QuerySelectorAll("script, style, nav, header, footer, aside");
            foreach (var element in scriptsAndStyles)
            {
                element.Remove();
            }

            // Try to find main content area
            var mainContent = document.QuerySelector("main, article, .content, .main-content, .post-content, #content");
            if (mainContent != null)
            {
                return CleanText(mainContent.TextContent);
            }

            // Fallback to body content
            var body = document.QuerySelector("body");
            return body != null ? CleanText(body.TextContent) : string.Empty;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to scrape website {url}: {ex.Message}", ex);
        }
    }    public async Task<string> ScrapeWebsiteAsync(string url, string selector)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(url);
            
            var config = AngleSharp.Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html));

            var elements = document.QuerySelectorAll(selector);
            var content = new StringBuilder();

            foreach (var element in elements)
            {
                content.AppendLine(CleanText(element.TextContent));
            }

            return content.ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to scrape website {url} with selector {selector}: {ex.Message}", ex);
        }
    }

    private string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Normalize whitespace and line breaks
        var lines = text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return string.Join("\n", lines);
    }
}
