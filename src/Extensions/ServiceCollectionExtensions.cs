using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using RagPoc.Services;
using RagPoc.Configuration;

namespace RagPoc.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRagServices(this IServiceCollection services, IConfiguration configuration)
    {        // Configuration
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.Configure<WebCrawlerOptions>(configuration.GetSection(WebCrawlerOptions.SectionName));// HTTP Client
        services.AddHttpClient<IWebScrapingService, WebScrapingService>();
        services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>();
        services.AddHttpClient<IRagService, RagService>();

        // Services
        services.AddSingleton<IDocumentProcessor, DocumentProcessorService>();
        services.AddSingleton<IVectorService, SqlServerVectorService>();

        return services;
    }
}
