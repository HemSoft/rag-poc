using Microsoft.Data.SqlClient;
using RagPoc.Models;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace RagPoc.Services;

public class SqlServerVectorService : IVectorService
{
    private readonly string _connectionString;
    private readonly IConfiguration _configuration;

    public SqlServerVectorService(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = GetWorkingConnectionString();
    }

    private string GetWorkingConnectionString()
    {
        var connectionStrings = new[]
        {
            _configuration.GetConnectionString("DefaultConnection"),
            _configuration.GetConnectionString("SqlServerExpress"),
            _configuration.GetConnectionString("LocalHost")
        };

        foreach (var connStr in connectionStrings.Where(cs => !string.IsNullOrEmpty(cs)))
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connStr!)
                {
                    IntegratedSecurity = true,
                    TrustServerCertificate = true,
                    ConnectTimeout = 30,
                    CommandTimeout = 60
                };

                // Test the connection
                using var connection = new SqlConnection(builder.ConnectionString);
                connection.Open();
                connection.Close();
                
                Console.WriteLine($"Successfully connected using: {builder.DataSource}");
                return builder.ConnectionString;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection attempt failed for {connStr}: {ex.Message}");
                continue;
            }
        }

        throw new InvalidOperationException("Could not establish database connection with Windows authentication. Please ensure SQL Server (LocalDB, Express, or full version) is installed and running.");
    }    public async Task InitializeDatabaseAsync()
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Create database if it doesn't exist
            var createDbSql = @"
                IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'RagPocDb')
                BEGIN
                    CREATE DATABASE RagPocDb;
                END";

            await using var createDbCmd = new SqlCommand(createDbSql, connection);
            await createDbCmd.ExecuteNonQueryAsync();

        // Switch to the RagPocDb database
        await connection.ChangeDatabaseAsync("RagPocDb");

        // Create Documents table
        var createDocumentsTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Documents')
            BEGIN
                CREATE TABLE Documents (
                    Id int IDENTITY(1,1) PRIMARY KEY,
                    FileName nvarchar(255) NOT NULL,
                    FilePath nvarchar(500) NOT NULL,
                    Content nvarchar(max) NOT NULL,
                    FileType nvarchar(50) NOT NULL,
                    CreatedAt datetime2 DEFAULT GETDATE()
                );
            END";

        await using var createDocumentsCmd = new SqlCommand(createDocumentsTableSql, connection);
        await createDocumentsCmd.ExecuteNonQueryAsync();        // Create DocumentChunks table - use nvarchar for embeddings (compatible with all SQL Server versions)
        var createChunksTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentChunks')
            BEGIN
                CREATE TABLE DocumentChunks (
                    Id int IDENTITY(1,1) PRIMARY KEY,
                    DocumentId int NOT NULL,
                    ChunkText nvarchar(max) NOT NULL,
                    ChunkIndex int NOT NULL,
                    Embedding nvarchar(max) NULL,
                    CreatedAt datetime2 DEFAULT GETDATE(),
                    FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
                );
            END";

        await using var createChunksCmd = new SqlCommand(createChunksTableSql, connection);
        await createChunksCmd.ExecuteNonQueryAsync();

        // Create vector index (if supported)
        try
        {
            var createIndexSql = @"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentChunks_Embedding')
                BEGIN
                    CREATE INDEX IX_DocumentChunks_Embedding ON DocumentChunks(Embedding);
                END";

            await using var createIndexCmd = new SqlCommand(createIndexSql, connection);
            await createIndexCmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Note: Could not create vector index (this is expected if not using SQL Server 2025): {ex.Message}");
        }        Console.WriteLine("Database initialized successfully!");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize database. Error: {ex.Message}. Please ensure SQL Server is running and you have proper Windows authentication permissions.", ex);
        }
    }

    public async Task<int> StoreDocumentAsync(Document document)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ChangeDatabaseAsync("RagPocDb");

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            // Insert document
            var insertDocSql = @"
                INSERT INTO Documents (FileName, FilePath, Content, FileType)
                OUTPUT INSERTED.Id
                VALUES (@FileName, @FilePath, @Content, @FileType)";

            int documentId;
            await using (var cmd = new SqlCommand(insertDocSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@FileName", document.FileName);
                cmd.Parameters.AddWithValue("@FilePath", document.FilePath);
                cmd.Parameters.AddWithValue("@Content", document.Content);
                cmd.Parameters.AddWithValue("@FileType", document.FileType);

                documentId = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            }

            // Insert chunks
            foreach (var chunk in document.Chunks)
            {
                var insertChunkSql = @"
                    INSERT INTO DocumentChunks (DocumentId, ChunkText, ChunkIndex, Embedding)
                    VALUES (@DocumentId, @ChunkText, @ChunkIndex, @Embedding)";

                await using var chunkCmd = new SqlCommand(insertChunkSql, connection, transaction);
                chunkCmd.Parameters.AddWithValue("@DocumentId", documentId);
                chunkCmd.Parameters.AddWithValue("@ChunkText", chunk.ChunkText);
                chunkCmd.Parameters.AddWithValue("@ChunkIndex", chunk.ChunkIndex);
                
                // Convert float array to JSON for storage (fallback for non-vector types)
                var embeddingJson = JsonSerializer.Serialize(chunk.Embedding);
                chunkCmd.Parameters.AddWithValue("@Embedding", embeddingJson);

                await chunkCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return documentId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }    public async Task<List<DocumentChunk>> SearchSimilarChunksAsync(float[] queryEmbedding, int maxResults = 5)
    {
        // Since we're using LocalDB/Express (not SQL Server 2025), use similarity calculation directly
        return await GetAllChunksWithSimilarityAsync(queryEmbedding, maxResults);
    }

    private async Task<List<DocumentChunk>> GetAllChunksWithSimilarityAsync(float[] queryEmbedding, int maxResults)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ChangeDatabaseAsync("RagPocDb");

        var allChunks = new List<(DocumentChunk chunk, double similarity)>();

        var sql = @"
            SELECT dc.Id, dc.DocumentId, dc.ChunkText, dc.ChunkIndex, dc.Embedding,
                   d.FileName, d.FilePath, d.FileType
            FROM DocumentChunks dc
            INNER JOIN Documents d ON dc.DocumentId = d.Id
            WHERE dc.Embedding IS NOT NULL";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();        while (await reader.ReadAsync())
        {
            var embeddingJson = reader.GetString(reader.GetOrdinal("Embedding"));
            var embedding = JsonSerializer.Deserialize<float[]>(embeddingJson);

            if (embedding != null && embedding.Length > 0)
            {
                var similarity = CalculateCosineSimilarity(queryEmbedding, embedding);
                
                var chunk = new DocumentChunk
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    DocumentId = reader.GetInt32(reader.GetOrdinal("DocumentId")),
                    ChunkText = reader.GetString(reader.GetOrdinal("ChunkText")),
                    ChunkIndex = reader.GetInt32(reader.GetOrdinal("ChunkIndex")),
                    Embedding = embedding,
                    Document = new Document
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("DocumentId")),
                        FileName = reader.GetString(reader.GetOrdinal("FileName")),
                        FilePath = reader.GetString(reader.GetOrdinal("FilePath")),
                        FileType = reader.GetString(reader.GetOrdinal("FileType"))
                    }
                };

                allChunks.Add((chunk, similarity));
            }
        }

        return allChunks
            .OrderByDescending(x => x.similarity)
            .Take(maxResults)
            .Select(x => x.chunk)
            .ToList();
    }

    private double CalculateCosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
            return 0;

        double dotProduct = 0;
        double norm1 = 0;
        double norm2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            norm1 += vector1[i] * vector1[i];
            norm2 += vector2[i] * vector2[i];
        }

        if (norm1 == 0 || norm2 == 0)
            return 0;

        return dotProduct / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
    }

    public async Task<List<Document>> GetAllDocumentsAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ChangeDatabaseAsync("RagPocDb");

        var documents = new List<Document>();

        var sql = @"
            SELECT Id, FileName, FilePath, FileType, CreatedAt
            FROM Documents
            ORDER BY CreatedAt DESC";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();        while (await reader.ReadAsync())
        {
            documents.Add(new Document
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                FileName = reader.GetString(reader.GetOrdinal("FileName")),
                FilePath = reader.GetString(reader.GetOrdinal("FilePath")),
                FileType = reader.GetString(reader.GetOrdinal("FileType")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            });
        }

        return documents;
    }

    public async Task<bool> DeleteDocumentAsync(int documentId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ChangeDatabaseAsync("RagPocDb");

        var sql = "DELETE FROM Documents WHERE Id = @DocumentId";
        
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@DocumentId", documentId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }
}
