namespace RagPoc.Configuration;

public class RagOptions
{
    public const string SectionName = "Rag";
    
    public int ChunkSize { get; set; } = 1000;
    public int ChunkOverlap { get; set; } = 200;
    public int MaxContextChunks { get; set; } = 5;
    public int MaxTokens { get; set; } = 2000;
}

public class OllamaOptions
{
    public const string SectionName = "Ollama";
    
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string ChatModel { get; set; } = "llama3.1:8b";
}
