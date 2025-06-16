# RAG POC - Retrieval Augmented Generation with SQL Server 2025 & Ollama

A console application demonstrating Retrieval Augmented Generation (RAG) using SQL Server 2025's vector features and local Ollama models.

## 🚀 Features

- **Document Processing**: PDF, DOCX, TXT, MD files
- **Web Scraping**: Extract content from websites
- **Local AI**: Uses Ollama for embeddings and chat (no API keys needed!)
- **Vector Search**: SQL Server 2025 vector database with similarity search
- **Interactive Chat**: Ask questions about your documents

## 🛠️ Prerequisites

### 1. SQL Server 2025 Preview
- Download and install [SQL Server 2025 Preview](https://info.microsoft.com/ww-landing-sql-server-2025.html)
- Make sure it's running and accessible on localhost

### 2. Ollama
- Install [Ollama](https://ollama.ai) 
- Pull the required models:
```bash
ollama pull nomic-embed-text
ollama pull llama3.1:8b
```

### 3. .NET 10
- Install [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)

## 📦 Setup

1. **Clone/Open the project**
2. **Restore packages**:
   ```bash
   dotnet restore
   ```

3. **Update connection string** (if needed) in `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=RagPocDb;Integrated Security=true;TrustServerCertificate=true;"
     }
   }
   ```

4. **Run the application**:
   ```bash
   dotnet run
   ```

## 🎯 Usage

### First Run
1. The app will automatically create the database and tables
2. Test Ollama connection (option 6) to ensure models are working

### Processing Documents
- **Option 1**: Process local files (PDF, DOCX, TXT, MD)
- **Option 2**: Process websites by URL

### Chatting
- **Option 3**: Ask questions about your processed documents
- The system will find relevant chunks and generate answers

### Management
- **Option 4**: List all processed documents
- **Option 5**: Delete documents from the database

## 🔧 Configuration

Edit `appsettings.json` to customize:

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text",
    "ChatModel": "llama3.1:8b"
  },
  "Rag": {
    "ChunkSize": 1000,
    "ChunkOverlap": 200,
    "MaxContextChunks": 5,
    "MaxTokens": 2000
  }
}
```

## 💡 Tips for Your Hardware (3080 Ti)

Your 3080 Ti (12GB VRAM) can handle:
- **Embedding model**: `nomic-embed-text` (~140MB)
- **Chat models**: 
  - `llama3.1:8b` (~4.7GB) - Recommended
  - `mistral:7b` (~4.1GB) - Alternative
  - `qwen2.5:7b` (~4.4GB) - Another good option

For even better performance, try:
- `llama3.1:70b` if you have enough system RAM to offload
- `qwen2.5:14b` for better quality with slightly more VRAM usage

## 🏗️ Architecture

```
Document/Web → Text Extraction → Chunking → Embeddings → SQL Server Vector DB
                                                              ↓
User Question → Embedding → Vector Search → Context → LLM → Response
```

## 🔍 SQL Server 2025 Vector Features Used

- `VECTOR` data type for storing embeddings
- `VECTOR_DISTANCE()` function for similarity search
- Vector indexes for performance

## 🐛 Troubleshooting

### Database Issues
- Ensure SQL Server 2025 is running
- Check connection string in appsettings.json
- Verify Windows Authentication or update to use SQL Auth

### Ollama Issues
- Verify Ollama is running: `ollama list`
- Pull missing models: `ollama pull model-name`
- Check models are loaded: Use option 6 in the app

### Memory Issues
- If models don't fit in VRAM, Ollama will use system RAM
- Consider smaller models if needed
- Monitor GPU memory usage

## 📚 Example Documents to Try

1. Research papers (PDF)
2. Technical documentation (MD)
3. News articles (web scraping)
4. Company documents (DOCX)

## 🚀 Next Steps

This is a POC foundation. You can extend it with:
- Web UI (Blazor/ASP.NET Core)
- Better chunking strategies
- Multiple document formats
- Advanced search filtering
- User management
- API endpoints

Enjoy exploring RAG with local models! 🎉
