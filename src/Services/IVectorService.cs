using RagPoc.Models;

namespace RagPoc.Services;

public interface IVectorService
{
    Task InitializeDatabaseAsync();
    Task<int> StoreDocumentAsync(Document document);
    Task<List<DocumentChunk>> SearchSimilarChunksAsync(float[] queryEmbedding, int maxResults = 5);
    Task<List<Document>> GetAllDocumentsAsync();
    Task<bool> DeleteDocumentAsync(int documentId);
}
