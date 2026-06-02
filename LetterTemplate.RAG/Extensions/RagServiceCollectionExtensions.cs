using LetterTemplate.RAG.Repositories;
using LetterTemplate.RAG.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LetterTemplate.RAG
{
    public static class RagServiceCollectionExtensions
    {
        public static IServiceCollection AddRagServices(this IServiceCollection services, IConfiguration configuration)
        {
            var ragSection = configuration.GetSection("Rag");
            var minChunkSize = ragSection.GetValue<int>("MinChunkSize");
            if (minChunkSize == 0) minChunkSize = 100;
            var maxChunkSize = ragSection.GetValue<int>("MaxChunkSize");
            if (maxChunkSize == 0) maxChunkSize = 500;

            services.AddScoped(_ => new ChunkingService(minChunkSize, maxChunkSize));
            services.AddScoped<EmbeddingService>();
            services.AddScoped<VectorRepository>();
            services.AddScoped<IRagService, RagService>();

            return services;
        }
    }
}
