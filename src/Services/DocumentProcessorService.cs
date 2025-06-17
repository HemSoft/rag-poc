using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using System.Text;
using System.Text.RegularExpressions;
using RagPoc.Configuration;
using Microsoft.Extensions.Options;

namespace RagPoc.Services;

public class DocumentProcessorService : IDocumentProcessor
{
    private readonly RagOptions _ragOptions;
    private readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".txt", ".md"
    };

    public DocumentProcessorService(IOptions<RagOptions> ragOptions)
    {
        _ragOptions = ragOptions.Value;
    }

    public bool CanProcess(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return _supportedExtensions.Contains(extension);
    }

    public async Task<string> ExtractTextAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var extension = Path.GetExtension(filePath).ToLowerInvariant();        return extension switch
        {
            ".pdf" => ExtractPdfText(filePath),
            ".docx" => ExtractDocxText(filePath),
            ".doc" => throw new NotSupportedException("Legacy .doc files are not supported. Please convert to .docx"),
            ".txt" => await File.ReadAllTextAsync(filePath),
            ".md" => await ExtractMarkdownTextAsync(filePath),
            _ => throw new NotSupportedException($"File type {extension} is not supported")
        };
    }

    public List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var chunks = new List<string>();
        var sentences = SplitIntoSentences(text);
        
        var currentChunk = new StringBuilder();
        var currentLength = 0;

        foreach (var sentence in sentences)
        {
            // If adding this sentence would exceed chunk size, finalize current chunk
            if (currentLength + sentence.Length > chunkSize && currentLength > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                
                // Start new chunk with overlap
                var overlapText = GetOverlapText(currentChunk.ToString(), overlap);
                currentChunk.Clear();
                currentChunk.Append(overlapText);
                currentLength = overlapText.Length;
            }

            currentChunk.Append(sentence);
            currentLength += sentence.Length;
        }

        // Add the last chunk if it has content
        if (currentLength > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }    private string ExtractPdfText(string filePath)
    {
        try
        {
            using var reader = new PdfDocument(new PdfReader(filePath));
            var text = new StringBuilder();

            for (int i = 1; i <= reader.GetNumberOfPages(); i++)
            {
                var page = reader.GetPage(i);
                var pageText = PdfTextExtractor.GetTextFromPage(page);
                text.AppendLine(pageText);
            }

            return text.ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to extract text from PDF: {ex.Message}", ex);
        }
    }    private string ExtractDocxText(string filePath)
    {
        try
        {
            using var document = WordprocessingDocument.Open(filePath, false);
            var body = document.MainDocumentPart?.Document.Body;
            
            if (body == null)
                return string.Empty;

            return body.InnerText ?? string.Empty;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to extract text from DOCX: {ex.Message}", ex);
        }
    }    private async Task<string> ExtractMarkdownTextAsync(string filePath)
    {
        try
        {
            var markdown = await File.ReadAllTextAsync(filePath);
            
            // Simple approach: Use Markdig to convert to HTML, then extract text
            // This preserves more content than trying to parse the AST
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var html = Markdown.ToHtml(markdown, pipeline);
            
            // Convert HTML to plain text by removing tags
            var plainText = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
            
            // Clean up extra whitespace
            plainText = System.Text.RegularExpressions.Regex.Replace(plainText, @"\s+", " ");
            plainText = System.Text.RegularExpressions.Regex.Replace(plainText, @"\n\s*\n", "\n\n");
            
            return plainText.Trim();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to extract text from Markdown: {ex.Message}", ex);
        }
    }    private List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var sentenceEnders = new[] { '.', '!', '?' };
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine))
                continue;

            // If line is short or doesn't end with sentence ender, treat as one sentence
            if (trimmedLine.Length < 100 || !sentenceEnders.Contains(trimmedLine.Last()))
            {
                sentences.Add(trimmedLine + " ");
                continue;
            }

            // Split by sentence enders but keep the punctuation
            var parts = new List<string>();
            var currentSentence = new StringBuilder();

            for (int i = 0; i < trimmedLine.Length; i++)
            {
                currentSentence.Append(trimmedLine[i]);
                
                if (sentenceEnders.Contains(trimmedLine[i]))
                {
                    // Check if this is likely the end of a sentence
                    if (i == trimmedLine.Length - 1 || 
                        (i < trimmedLine.Length - 1 && char.IsWhiteSpace(trimmedLine[i + 1])))
                    {
                        parts.Add(currentSentence.ToString().Trim() + " ");
                        currentSentence.Clear();
                    }
                }
            }

            if (currentSentence.Length > 0)
            {
                parts.Add(currentSentence.ToString().Trim() + " ");
            }

            sentences.AddRange(parts);
        }

        return sentences;
    }

    private string GetOverlapText(string text, int overlapLength)
    {
        if (text.Length <= overlapLength)
            return text;

        // Try to break at sentence boundary within overlap range
        var overlapText = text.Substring(text.Length - overlapLength);
        var sentences = SplitIntoSentences(overlapText);
        
        if (sentences.Count > 1)
        {
            // Return the last complete sentence(s) within overlap
            return string.Join("", sentences.Skip(1));
        }

        return overlapText;
    }
}
