using RagPoc.Models;

namespace RagPoc.Services;

public interface IRagService
{
    Task<string> ProcessDocumentAsync(string filePath);
    Task<string> ProcessWebContentAsync(string url);
    Task<ChatMessage> ChatAsync(string question);
    Task<List<Document>> GetDocumentsAsync();
    Task<bool> DeleteDocumentAsync(int documentId);
}
