using RagPoc.Configuration;
using RagPoc.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace RagPoc.Services;

public class RagService : IRagService
{
    private readonly IDocumentProcessor _documentProcessor;
    private readonly IWebScrapingService _webScrapingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorService _vectorService;
    private readonly HttpClient _httpClient;
    private readonly RagOptions _ragOptions;
    private readonly OllamaOptions _ollamaOptions;

    public RagService(
        IDocumentProcessor documentProcessor,
        IWebScrapingService webScrapingService,
        IEmbeddingService embeddingService,
        IVectorService vectorService,
        HttpClient httpClient,
        IOptions<RagOptions> ragOptions,
        IOptions<OllamaOptions> ollamaOptions)
    {
        _documentProcessor = documentProcessor;
        _webScrapingService = webScrapingService;
        _embeddingService = embeddingService;
        _vectorService = vectorService;
        _httpClient = httpClient;
        _ragOptions = ragOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
        _httpClient.BaseAddress = new Uri(_ollamaOptions.BaseUrl);
    }

    public async Task<string> ProcessDocumentAsync(string filePath)
    {
        try
        {
            if (!_documentProcessor.CanProcess(filePath))
            {
                return $"File type not supported: {Path.GetExtension(filePath)}";
            }

            Console.WriteLine($"Processing document: {Path.GetFileName(filePath)}");
            
            // Extract text from document
            var content = await _documentProcessor.ExtractTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(content))
            {
                return "No content extracted from document";
            }

            Console.WriteLine($"Extracted {content.Length} characters");

            // Create document model
            var document = new Document
            {
                FileName = Path.GetFileName(filePath),
                FilePath = filePath,
                Content = content,
                FileType = Path.GetExtension(filePath).TrimStart('.')
            };

            // Chunk the text
            var chunks = _documentProcessor.ChunkText(content, _ragOptions.ChunkSize, _ragOptions.ChunkOverlap);
            Console.WriteLine($"Created {chunks.Count} chunks");

            // Generate embeddings for chunks (documents)
            Console.WriteLine("Generating embeddings...");
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks, "search_document");

            // Create document chunks
            for (int i = 0; i < chunks.Count; i++)
            {
                document.Chunks.Add(new DocumentChunk
                {
                    ChunkText = chunks[i],
                    ChunkIndex = i,
                    Embedding = embeddings[i]
                });
            }

            // Store in database
            var documentId = await _vectorService.StoreDocumentAsync(document);
            
            return $"Successfully processed document '{document.FileName}' with ID {documentId}. Created {chunks.Count} chunks.";
        }
        catch (Exception ex)
        {
            return $"Error processing document: {ex.Message}";
        }
    }    public async Task<string> ProcessWebContentAsync(string url)
    {
        try
        {
            Console.WriteLine($"🌐 Processing website: {url}");
            Console.Write("Do you want to crawl multiple pages? (y/N): ");
            var crawlResponse = Console.ReadLine()?.ToLowerInvariant();
            
            string content;
            string documentTitle;
            
            if (crawlResponse == "y" || crawlResponse == "yes")
            {
                Console.WriteLine("🕷️ Starting smart crawl...");
                content = await _webScrapingService.CrawlWebsiteAsync(url);
                documentTitle = $"Crawled Content";
            }
            else
            {
                Console.WriteLine("📄 Scraping single page...");
                content = await _webScrapingService.ScrapeWebsiteAsync(url);
                documentTitle = GetPageTitle(content);
            }
            
            if (string.IsNullOrWhiteSpace(content))
            {
                return "No content extracted from website";
            }            Console.WriteLine($"Extracted {content.Length} characters");            // Create document model
            var document = new Document
            {
                FileName = GenerateUniqueWebFileName(url, documentTitle),
                FilePath = url,
                Content = content,
                FileType = "web"
            };

            // Chunk the text
            var chunks = _documentProcessor.ChunkText(content, _ragOptions.ChunkSize, _ragOptions.ChunkOverlap);
            Console.WriteLine($"Created {chunks.Count} chunks");            // Generate embeddings for chunks
            Console.WriteLine("Generating embeddings...");
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks, "search_document");

            // Create document chunks
            for (int i = 0; i < chunks.Count; i++)
            {
                document.Chunks.Add(new DocumentChunk
                {
                    ChunkText = chunks[i],
                    ChunkIndex = i,
                    Embedding = embeddings[i]
                });
            }

            // Store in database
            var documentId = await _vectorService.StoreDocumentAsync(document);
            
            return $"Successfully processed website '{document.FileName}' with ID {documentId}. Created {chunks.Count} chunks.";
        }
        catch (Exception ex)
        {
            return $"Error processing website: {ex.Message}";
        }
    }

    public async Task<ChatMessage> ChatAsync(string question)
    {
        try
        {            Console.WriteLine("Generating embedding for question...");
            
            // Generate embedding for the question (query)
            var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(question, "search_query");
            if (questionEmbedding == null || questionEmbedding.Length == 0)
            {
                return new ChatMessage
                {
                    Role = "assistant",
                    Content = "Sorry, I couldn't process your question. Please try again."
                };
            }

            Console.WriteLine("Searching for relevant context...");
            
            // Find similar chunks
            var similarChunks = await _vectorService.SearchSimilarChunksAsync(questionEmbedding, _ragOptions.MaxContextChunks);
            
            if (!similarChunks.Any())
            {
                return new ChatMessage
                {
                    Role = "assistant",
                    Content = "I don't have any relevant information to answer your question. Please add some documents first."
                };
            }

            Console.WriteLine($"Found {similarChunks.Count} relevant chunks");            // Build context from similar chunks
            var contextBuilder = new StringBuilder();
            var sources = new List<string>();
            
            foreach (var chunk in similarChunks)
            {
                // Show source with URL for web documents
                var sourceInfo = chunk.Document.FileType == "web" 
                    ? $"{chunk.Document.FileName} ({chunk.Document.FilePath})"
                    : chunk.Document.FileName;
                    
                contextBuilder.AppendLine($"Source: {sourceInfo}");
                contextBuilder.AppendLine(chunk.ChunkText);
                contextBuilder.AppendLine();
                
                if (!sources.Contains(sourceInfo))
                {
                    sources.Add(sourceInfo);
                }
            }

            // Create the prompt
            var prompt = $@"Based on the following context, please answer the question. If the answer is not in the context, say so.

Context:
{contextBuilder}

Question: {question}

Answer:";            Console.WriteLine("Generating response...");

            // Generate response using Ollama HTTP API
            var requestBody = new
            {
                model = _ollamaOptions.ChatModel,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                stream = false,
                options = new
                {
                    temperature = 0.7,
                    num_ctx = _ragOptions.MaxTokens
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpResponse = await _httpClient.PostAsync("/api/chat", content);
            httpResponse.EnsureSuccessStatusCode();

            var responseJson = await httpResponse.Content.ReadAsStringAsync();
            var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson);

            return new ChatMessage
            {
                Role = "assistant",
                Content = chatResponse?.message?.content ?? "Sorry, I couldn't generate a response.",
                Sources = sources
            };
        }
        catch (Exception ex)
        {
            return new ChatMessage
            {
                Role = "assistant",
                Content = $"Error generating response: {ex.Message}"
            };
        }
    }

    private string GetPageTitle(string content)
    {
        try
        {
            // Simple extraction of title from HTML content
            var titleMatch = System.Text.RegularExpressions.Regex.Match(content, @"<title[^>]*>([^<]+)</title>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (titleMatch.Success)
            {
                return titleMatch.Groups[1].Value.Trim();
            }
            
            // Fallback: look for h1 tag
            var h1Match = System.Text.RegularExpressions.Regex.Match(content, @"<h1[^>]*>([^<]+)</h1>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (h1Match.Success)
            {
                return h1Match.Groups[1].Value.Trim();
            }
            
            return "Web Page";
        }
        catch
        {
            return "Web Page";
        }
    }    private string GenerateUniqueWebFileName(string url, string documentTitle)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;
            var path = uri.AbsolutePath;
            var query = uri.Query;
            
            // Clean up the document title (remove/replace problematic characters)
            var cleanTitle = CleanFileName(documentTitle);
            
            // Create a more descriptive and unique filename using the full URL path
            var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            // For root path, use host + title + timestamp for uniqueness
            if (pathSegments.Length == 0)
            {
                return $"{host} - {cleanTitle} - {DateTime.UtcNow:yyyyMMdd-HHmmss}";
            }
            
            // Build a meaningful path representation
            var meaningfulPath = string.Join("-", pathSegments.Take(4)); // Take up to 4 segments to avoid too long names
            var cleanPath = CleanFileName(meaningfulPath);
            
            // Include query parameters if they exist and are short enough
            var queryPart = "";
            if (!string.IsNullOrEmpty(query) && query.Length <= 50)
            {
                queryPart = " " + CleanFileName(query);
            }
            
            // Create filename with host, path, and title for maximum uniqueness
            var baseFileName = $"{host} - {cleanPath} - {cleanTitle}{queryPart}";
            
            // If the filename is too long, truncate intelligently
            if (baseFileName.Length > 150)
            {
                // Try with shorter path (first 2 segments)
                var shorterPath = string.Join("-", pathSegments.Take(2));
                var cleanShorterPath = CleanFileName(shorterPath);
                baseFileName = $"{host} - {cleanShorterPath} - {cleanTitle}";
                
                // If still too long, use hash of full path
                if (baseFileName.Length > 150)
                {
                    var pathHash = Math.Abs(path.GetHashCode()).ToString("X6");
                    baseFileName = $"{host} - {pathHash} - {cleanTitle}";
                }
            }
            
            return baseFileName;
        }
        catch
        {
            // Fallback to simple format if URL parsing fails
            return $"Web Content - {CleanFileName(documentTitle)} - {DateTime.UtcNow:yyyyMMdd-HHmmss}";
        }
    }
      private string CleanFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "Untitled";
            
        // Remove or replace characters that are problematic in filenames or database
        var cleaned = input.Trim()
            .Replace('/', '-')
            .Replace('\\', '-')
            .Replace(':', '-')
            .Replace('*', '-')
            .Replace('?', '_')
            .Replace('"', '\'')
            .Replace('<', '(')
            .Replace('>', ')')
            .Replace('|', '-')
            .Replace('#', '-')
            .Replace('%', '-')
            .Replace('&', '_')
            .Replace('=', '_');
            
        // Replace multiple consecutive dashes/underscores with single ones
        while (cleaned.Contains("--"))
            cleaned = cleaned.Replace("--", "-");
        while (cleaned.Contains("__"))
            cleaned = cleaned.Replace("__", "_");
        
        // Remove leading/trailing dashes and underscores
        cleaned = cleaned.Trim('-', '_');
        
        // Limit length to reasonable size
        if (cleaned.Length > 80)
        {
            cleaned = cleaned.Substring(0, 80).Trim('-', '_');
        }
        
        return string.IsNullOrWhiteSpace(cleaned) ? "Untitled" : cleaned;
    }

    public async Task<List<Document>> GetDocumentsAsync()
    {
        return await _vectorService.GetAllDocumentsAsync();
    }    public async Task<bool> DeleteDocumentAsync(int documentId)
    {
        return await _vectorService.DeleteDocumentAsync(documentId);
    }
}

public class OllamaChatResponse
{
    public OllamaChatMessage? message { get; set; }
}

public class OllamaChatMessage
{
    public string content { get; set; } = string.Empty;
}
