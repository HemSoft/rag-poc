using RagPoc.Models;

namespace RagPoc.Services;

public interface IDocumentProcessor
{
    Task<string> ExtractTextAsync(string filePath);
    bool CanProcess(string filePath);
    List<string> ChunkText(string text, int chunkSize, int overlap);
}
