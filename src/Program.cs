using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using RagPoc.Extensions;
using RagPoc.Services;
using System.Text;
using System.Text.Json;

namespace RagPoc;

class Program
{
    static async Task Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Build host
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddRagServices(configuration);
            })
            .Build();

        // Get services
        var ragService = host.Services.GetRequiredService<IRagService>();
        var vectorService = host.Services.GetRequiredService<IVectorService>();

        Console.WriteLine("🚀 RAG POC - Retrieval Augmented Generation with SQL Server 2025 & Ollama");
        Console.WriteLine("===============================================================================");
        Console.WriteLine();

        try
        {
            // Initialize database
            Console.WriteLine("Initializing database...");
            Console.WriteLine("Testing database connection with Windows authentication...");
            await vectorService.InitializeDatabaseAsync();
            Console.WriteLine("✅ Database ready!");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
            Console.WriteLine("Please make sure SQL Server 2025 is running and accessible.");
            return;
        }

        // Main loop
        while (true)
        {
            ShowMenu();
            var choice = Console.ReadLine()?.Trim();

            try
            {
                switch (choice?.ToLower())
                {
                    case "1":
                        await ProcessDocument(ragService);
                        break;
                    case "2":
                        await ProcessWebsite(ragService);
                        break;
                    case "3":
                        await ChatWithDocuments(ragService);
                        break;
                    case "4":
                        await ListDocuments(ragService);
                        break;
                    case "5":
                        await DeleteDocument(ragService);
                        break;
                    case "6":
                        await TestOllamaConnection();
                        break;
                    case "q":
                    case "quit":
                    case "exit":
                        Console.WriteLine("👋 Goodbye!");
                        return;
                    default:
                        Console.WriteLine("❌ Invalid option. Please try again.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
            Console.Clear();
        }
    }

    static void ShowMenu()
    {
        Console.WriteLine("📚 RAG POC - Main Menu");
        Console.WriteLine("=====================");
        Console.WriteLine("1. 📄 Process Document (PDF, DOCX, TXT, MD)");
        Console.WriteLine("2. 🌐 Process Website");
        Console.WriteLine("3. 💬 Chat with Documents");
        Console.WriteLine("4. 📋 List Documents");
        Console.WriteLine("5. 🗑️  Delete Document");
        Console.WriteLine("6. 🔧 Test Ollama Connection");
        Console.WriteLine("Q. ❌ Quit");
        Console.WriteLine();
        Console.Write("Choose an option: ");
    }

    static async Task ProcessDocument(IRagService ragService)
    {
        Console.WriteLine("\n📄 Process Document");
        Console.WriteLine("==================");
        Console.Write("Enter file path: ");
        var filePath = Console.ReadLine()?.Trim().Trim('"');

        if (string.IsNullOrEmpty(filePath))
        {
            Console.WriteLine("❌ File path cannot be empty.");
            return;
        }

        if (!File.Exists(filePath))
        {
            Console.WriteLine("❌ File not found.");
            return;
        }

        Console.WriteLine("\n🔄 Processing document...");
        var result = await ragService.ProcessDocumentAsync(filePath);
        Console.WriteLine($"✅ {result}");
    }

    static async Task ProcessWebsite(IRagService ragService)
    {
        Console.WriteLine("\n🌐 Process Website");
        Console.WriteLine("=================");
        Console.Write("Enter website URL: ");
        var url = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(url))
        {
            Console.WriteLine("❌ URL cannot be empty.");
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            Console.WriteLine("❌ Invalid URL format.");
            return;
        }

        Console.WriteLine("\n🔄 Processing website...");
        var result = await ragService.ProcessWebContentAsync(url);
        Console.WriteLine($"✅ {result}");
    }

    static async Task ChatWithDocuments(IRagService ragService)
    {
        Console.WriteLine("\n💬 Chat with Documents");
        Console.WriteLine("=====================");
        Console.WriteLine("Ask questions about your documents. Type 'back' to return to main menu.");
        Console.WriteLine();

        while (true)
        {
            Console.Write("You: ");
            var question = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(question))
                continue;

            if (question.ToLower() == "back")
                break;

            Console.WriteLine("\n🤔 Thinking...");
            var response = await ragService.ChatAsync(question);

            Console.WriteLine($"\n🤖 Assistant: {response.Content}");
            
            if (response.Sources != null && response.Sources.Any())
            {
                Console.WriteLine($"\n📚 Sources: {string.Join(", ", response.Sources)}");
            }
            
            Console.WriteLine();
        }
    }

    static async Task ListDocuments(IRagService ragService)
    {
        Console.WriteLine("\n📋 Documents in Database");
        Console.WriteLine("=======================");

        var documents = await ragService.GetDocumentsAsync();

        if (!documents.Any())
        {
            Console.WriteLine("No documents found. Add some documents first!");
            return;
        }

        Console.WriteLine($"{"ID",-5} {"Name",-30} {"Type",-10} {"Created",-20}");
        Console.WriteLine(new string('-', 70));

        foreach (var doc in documents)
        {
            Console.WriteLine($"{doc.Id,-5} {doc.FileName,-30} {doc.FileType,-10} {doc.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        Console.WriteLine($"\nTotal: {documents.Count} documents");
    }

    static async Task DeleteDocument(IRagService ragService)
    {
        Console.WriteLine("\n🗑️ Delete Document");
        Console.WriteLine("==================");

        // First show documents
        await ListDocuments(ragService);
        
        Console.WriteLine();
        Console.Write("Enter document ID to delete (or 0 to cancel): ");
        
        if (!int.TryParse(Console.ReadLine(), out int documentId) || documentId <= 0)
        {
            Console.WriteLine("❌ Invalid document ID.");
            return;
        }

        Console.Write($"Are you sure you want to delete document ID {documentId}? (y/N): ");
        var confirmation = Console.ReadLine()?.Trim().ToLower();

        if (confirmation != "y" && confirmation != "yes")
        {
            Console.WriteLine("❌ Deletion cancelled.");
            return;
        }

        var result = await ragService.DeleteDocumentAsync(documentId);
        if (result)
        {
            Console.WriteLine("✅ Document deleted successfully.");
        }
        else
        {
            Console.WriteLine("❌ Document not found or could not be deleted.");
        }
    }    static async Task TestOllamaConnection()
    {
        Console.WriteLine("\n🔧 Testing Ollama Connection");
        Console.WriteLine("============================");

        try
        {
            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("http://localhost:11434");
            
            Console.WriteLine("📡 Testing connection to Ollama...");
            
            // Test basic connection
            var response = await httpClient.GetAsync("/api/tags");
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var modelsResponse = System.Text.Json.JsonSerializer.Deserialize<OllamaModelsResponse>(responseJson);
            
            Console.WriteLine("✅ Connected to Ollama successfully!");
            Console.WriteLine($"📦 Available models ({modelsResponse?.models?.Length ?? 0}):");
            
            if (modelsResponse?.models != null)
            {
                foreach (var model in modelsResponse.models)
                {
                    var sizeGB = model.size / (1024.0 * 1024.0 * 1024.0);
                    Console.WriteLine($"   • {model.name} ({sizeGB:F1} GB)");
                }
            }

            // Test embedding model
            Console.WriteLine("\n🧮 Testing embedding model...");
            try
            {                var embeddingRequest = new
                {
                    model = "nomic-embed-text",
                    prompt = "Hello world"
                };

                var json = JsonSerializer.Serialize(embeddingRequest);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var embeddingResponse = await httpClient.PostAsync("/api/embeddings", content);
                embeddingResponse.EnsureSuccessStatusCode();                var embeddingJson = await embeddingResponse.Content.ReadAsStringAsync();
                var embedding = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(embeddingJson);

                if (embedding?.embedding != null && embedding.embedding.Length > 0)
                {
                    Console.WriteLine($"✅ Embedding model working! Generated {embedding.embedding.Length} dimensions");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Embedding model test failed: {ex.Message}");
                Console.WriteLine("Make sure 'nomic-embed-text' is installed: ollama pull nomic-embed-text");
            }

            // Test chat model
            Console.WriteLine("\n💬 Testing chat model...");
            try
            {
                var chatRequest = new
                {
                    model = "llama3.1:8b",
                    messages = new[]
                    {
                        new { role = "user", content = "Say hello in one word." }
                    },
                    stream = false
                };                var chatJson = JsonSerializer.Serialize(chatRequest);
                using var chatContent = new StringContent(chatJson, Encoding.UTF8, "application/json");
                
                var chatResponse = await httpClient.PostAsync("/api/chat", chatContent);
                chatResponse.EnsureSuccessStatusCode();                var chatResponseJson = await chatResponse.Content.ReadAsStringAsync();
                var chat = JsonSerializer.Deserialize<OllamaChatResponse>(chatResponseJson);

                Console.WriteLine($"✅ Chat model working! Response: {chat?.message?.content ?? "No response"}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Chat model test failed: {ex.Message}");
                Console.WriteLine("Make sure 'llama3.1:8b' is installed: ollama pull llama3.1:8b");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to connect to Ollama: {ex.Message}");
            Console.WriteLine("Make sure Ollama is running on http://localhost:11434");
            Console.WriteLine("Install from: https://ollama.ai");
        }
    }
}

public class OllamaModelsResponse
{
    public OllamaModel[]? models { get; set; }
}

public class OllamaModel
{
    public string name { get; set; } = string.Empty;
    public long size { get; set; }
}

public class OllamaEmbeddingResponse
{
    public double[]? embedding { get; set; }
}

public class OllamaChatResponse
{
    public OllamaMessage? message { get; set; }
}

public class OllamaMessage
{
    public string role { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
}
