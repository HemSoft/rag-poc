using AngleSharp;
using AngleSharp.Html.Dom;
using System.Text;
using RagPoc.Configuration;
using Microsoft.Extensions.Options;
using Abot2.Core;
using Abot2.Crawler;
using Abot2.Poco;
using System.Collections.Concurrent;

namespace RagPoc.Services;

public class WebScrapingService : IWebScrapingService
{
    private readonly HttpClient _httpClient;
    private readonly WebCrawlerOptions _crawlerOptions;

    public WebScrapingService(HttpClient httpClient, IOptions<WebCrawlerOptions> crawlerOptions)
    {
        _httpClient = httpClient;
        _crawlerOptions = crawlerOptions.Value;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }public async Task<string> ScrapeWebsiteAsync(string url)
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
    }    public async Task<string> CrawlWebsiteAsync(string startUrl)
    {
        var crawledContent = new ConcurrentDictionary<string, string>();
        var contentBuilder = new StringBuilder();
        
        Console.WriteLine($"Starting Abot crawl from: {startUrl}");
        Console.WriteLine($"Max depth: {_crawlerOptions.MaxDepth}, Max pages: {_crawlerOptions.MaxPages}");
        
        // Configure Abot crawler
        var config = new CrawlConfiguration
        {
            MaxConcurrentThreads = 1, // Keep it simple and respectful
            MaxPagesToCrawl = _crawlerOptions.MaxPages,
            MaxCrawlDepth = _crawlerOptions.MaxDepth,
            MinCrawlDelayPerDomainMilliSeconds = _crawlerOptions.DelayBetweenRequests,
            IsUriRecrawlingEnabled = false,
            IsExternalPageCrawlingEnabled = !_crawlerOptions.SameOriginOnly,
            UserAgentString = "RagPoc-Crawler/1.0 (Educational/Research Purpose)",
            HttpRequestTimeoutInSeconds = 30,
            HttpServicePointConnectionLimit = 5,
            IsSendingCookiesEnabled = false,
            IsRespectRobotsDotTextEnabled = true
        };

        var crawler = new PoliteWebCrawler(config);

        // Handle successful page crawls
        crawler.PageCrawlCompleted += (sender, e) =>
        {
            var crawledPage = e.CrawledPage;
            
            if (crawledPage.HttpResponseMessage?.IsSuccessStatusCode == true && 
                crawledPage.Content?.Text != null)
            {
                try
                {
                    Console.WriteLine($"Successfully crawled: {crawledPage.Uri} (depth: {crawledPage.CrawlDepth})");
                    
                    // Extract page title and content
                    var pageTitle = ExtractPageTitle(crawledPage.Content.Text);
                    var pageContent = ExtractMainContent(crawledPage.Content.Text);
                    
                    if (!string.IsNullOrWhiteSpace(pageContent))
                    {
                        var formattedContent = $"\n=== {pageTitle} ===\nURL: {crawledPage.Uri}\n\n{pageContent}\n";
                        crawledContent.TryAdd(crawledPage.Uri.ToString(), formattedContent);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing page {crawledPage.Uri}: {ex.Message}");
                }
            }
        };

        // Handle crawl decision (what links to follow)
        crawler.ShouldCrawlPageDecisionMaker = (pageToCrawl, crawlContext) =>
        {
            var decision = new CrawlDecision { Allow = true };
            
            var uri = pageToCrawl.Uri;
            var startUri = new Uri(startUrl);
            
            // Apply same origin check if enabled
            if (_crawlerOptions.SameOriginOnly && !string.Equals(uri.Host, startUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                decision.Allow = false;
                decision.Reason = $"Different host: {uri.Host} != {startUri.Host}";
                return decision;
            }
              // Filter out non-content links
            var urlString = uri.ToString().ToLowerInvariant();
            var excludePatterns = new[] { 
                ".pdf", ".doc", ".docx", ".zip", ".exe", ".dmg", 
                "javascript:", "mailto:", "tel:", "#", 
                "/api/", "/browse/?", "?tab=", "&tab=",
                "/contribute/", "/legal/", "/privacy/", "/lifecycle/",
                "/previous-versions/", "/troubleshoot/"
            };
            
            foreach (var pattern in excludePatterns)
            {
                if (urlString.Contains(pattern))
                {
                    decision.Allow = false;
                    decision.Reason = $"Contains excluded pattern: {pattern}";
                    return decision;
                }
            }
            
            // Custom path-based filtering (similar to original logic)
            if (!IsValidCrawlPath(uri, startUri))
            {
                decision.Allow = false;
                decision.Reason = "Path doesn't match crawling criteria";
                return decision;
            }
            
            Console.WriteLine($"✅ Allowing crawl: {uri}");
            return decision;
        };

        // Handle errors
        crawler.PageCrawlDisallowed += (sender, e) =>
        {
            Console.WriteLine($"❌ Crawl disallowed: {e.PageToCrawl.Uri} - {e.DisallowedReason}");
        };

        crawler.PageCrawlCompleted += (sender, e) =>
        {
            if (!e.CrawledPage.HttpResponseMessage?.IsSuccessStatusCode == true)
            {
                Console.WriteLine($"❌ Failed to crawl: {e.CrawledPage.Uri} - Status: {e.CrawledPage.HttpResponseMessage?.StatusCode}");
            }
        };

        // Start crawling
        var crawlResult = await crawler.CrawlAsync(new Uri(startUrl));
        
        Console.WriteLine($"Crawl completed. Crawled {crawlResult.CrawlContext.CrawledCount} pages");
        
        // Combine all crawled content
        foreach (var content in crawledContent.Values.OrderBy(c => c))
        {
            contentBuilder.Append(content);
        }
        
        return contentBuilder.ToString();
    }    private bool IsValidCrawlPath(Uri linkUri, Uri baseUri)
    {
        var basePath = GetBasePath(baseUri);
        var linkPath = GetBasePath(linkUri);
        
        // Check if it's a learning/documentation platform
        if (IsLearningPlatform(baseUri))
        {
            return IsValidLearningPath(linkPath, basePath);
        }
        
        // For other sites, use the original logic: must start with base path (not go "up" in hierarchy)
        return linkPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsLearningPlatform(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        var learningDomains = new[]
        {
            "learn.microsoft.com",
            "docs.microsoft.com",
            "developer.mozilla.org",
            "devdocs.io",
            "readthedocs.io",
            "gitbook.io"
        };
        
        return learningDomains.Any(domain => host.Contains(domain));
    }

    private bool IsValidLearningPath(string linkPath, string basePath)
    {
        // For learning platforms, be more flexible about paths
        var learningPathPatterns = new[]
        {
            "/training/paths/",
            "/training/modules/",
            "/docs/",
            "/learn/",
            "/guide/",
            "/tutorial/",
            "/documentation/"
        };
        
        // Allow any path that contains learning-related segments
        foreach (var pattern in learningPathPatterns)
        {
            if (linkPath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        // Fallback to original logic
        return linkPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
    }

    private string ExtractPageTitle(string html)
    {
        try
        {
            var config = AngleSharp.Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = context.OpenAsync(req => req.Content(html)).Result;
            
            var titleElement = document.QuerySelector("title");
            if (titleElement != null && !string.IsNullOrWhiteSpace(titleElement.TextContent))
            {
                return CleanText(titleElement.TextContent);
            }
            
            // Fallback to h1
            var h1Element = document.QuerySelector("h1");
            if (h1Element != null && !string.IsNullOrWhiteSpace(h1Element.TextContent))
            {
                return CleanText(h1Element.TextContent);
            }
            
            return "Untitled Page";
        }
        catch
        {
            return "Untitled Page";
        }
    }

    private string ExtractMainContent(string html)
    {
        try
        {
            var config = AngleSharp.Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = context.OpenAsync(req => req.Content(html)).Result;

            // Remove script and style elements
            var scriptsAndStyles = document.QuerySelectorAll("script, style, nav, header, footer, aside, .navigation, .sidebar");
            foreach (var element in scriptsAndStyles)
            {
                element.Remove();
            }

            // Try to find main content area
            var mainContent = document.QuerySelector("main, article, .content, .main-content, .post-content, #content, .documentation");
            if (mainContent != null)
            {
                return CleanText(mainContent.TextContent);
            }

            // Fallback to body content
            var body = document.QuerySelector("body");
            return body != null ? CleanText(body.TextContent) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }    private string GetBasePath(Uri uri)
    {
        var path = uri.AbsolutePath;
        if (path.EndsWith("/"))
            path = path.TrimEnd('/');
        return path;
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
