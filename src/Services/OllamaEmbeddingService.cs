using RagPoc.Configuration;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace RagPoc.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    public OllamaEmbeddingService(HttpClient httpClient, IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
    }    public async Task<float[]> GenerateEmbeddingAsync(string text, string taskType = "search_document")
    {
        try
        {
            // Add task prefix for nomic-embed-text model
            var prefixedText = $"{taskType}: {text}";
            
            var requestBody = new
            {
                model = _options.EmbeddingModel,
                prompt = prefixedText
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/embeddings", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var embeddingResponse = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseJson);

            return embeddingResponse?.embedding?.Select(d => (float)d).ToArray() ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating embedding: {ex.Message}");
            return Array.Empty<float>();
        }
    }    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, string taskType = "search_document")
    {
        var embeddings = new List<float[]>();
        
        foreach (var text in texts)
        {
            var embedding = await GenerateEmbeddingAsync(text, taskType);
            embeddings.Add(embedding);
            
            // Small delay to avoid overwhelming the local Ollama instance
            await Task.Delay(100);
        }
        
        return embeddings;
    }
}

public class OllamaEmbeddingResponse
{
    public double[]? embedding { get; set; }
}
