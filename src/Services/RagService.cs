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

            // Generate embeddings for chunks
            Console.WriteLine("Generating embeddings...");
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks);

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
    }

    public async Task<string> ProcessWebContentAsync(string url)
    {
        try
        {
            Console.WriteLine($"Scraping website: {url}");
            
            // Scrape content from website
            var content = await _webScrapingService.ScrapeWebsiteAsync(url);
            if (string.IsNullOrWhiteSpace(content))
            {
                return "No content extracted from website";
            }

            Console.WriteLine($"Extracted {content.Length} characters");

            // Create document model
            var document = new Document
            {
                FileName = new Uri(url).Host,
                FilePath = url,
                Content = content,
                FileType = "web"
            };

            // Chunk the text
            var chunks = _documentProcessor.ChunkText(content, _ragOptions.ChunkSize, _ragOptions.ChunkOverlap);
            Console.WriteLine($"Created {chunks.Count} chunks");

            // Generate embeddings for chunks
            Console.WriteLine("Generating embeddings...");
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks);

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
        {
            Console.WriteLine("Generating embedding for question...");
            
            // Generate embedding for the question
            var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(question);
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

            Console.WriteLine($"Found {similarChunks.Count} relevant chunks");

            // Build context from similar chunks
            var contextBuilder = new StringBuilder();
            var sources = new List<string>();
            
            foreach (var chunk in similarChunks)
            {
                contextBuilder.AppendLine($"Source: {chunk.Document.FileName}");
                contextBuilder.AppendLine(chunk.ChunkText);
                contextBuilder.AppendLine();
                
                if (!sources.Contains(chunk.Document.FileName))
                {
                    sources.Add(chunk.Document.FileName);
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
