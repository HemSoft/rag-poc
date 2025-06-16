namespace RagPoc.Models;

public class DocumentChunk
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Document Document { get; set; } = null!;
}
