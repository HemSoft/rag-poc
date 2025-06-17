namespace RagPoc.Services;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, string taskType = "search_document");
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, string taskType = "search_document");
}
