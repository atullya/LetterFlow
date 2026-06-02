using LetterTemplate.RAG.Models;
using Microsoft.EntityFrameworkCore;
using Logging;
using Npgsql;
using Pgvector;

namespace LetterTemplate.RAG.Repositories
{
    public class VectorRepository
    {
        private readonly DbContext _context;
        private readonly IAppLogger _logger;

        public VectorRepository(DbContext context, IAppLogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public bool IsAvailable => PgVectorAvailability.IsAvailable;

        public async Task IngestChunksAsync(int postId, List<string> chunks, List<float[]> embeddings, string title, string authorName, string slug)
        {
            if (!PgVectorAvailability.IsAvailable)
            {
                _logger.LogWarning("VectorRepository", "Cannot ingest chunks — pgvector extension not available.");
                return;
            }

            await DeleteChunksAsync(postId);

            var entities = new List<BlogChunk>(chunks.Count);
            for (var i = 0; i < chunks.Count; i++)
            {
                entities.Add(new BlogChunk
                {
                    PostId = postId,
                    Content = chunks[i],
                    ChunkIndex = i,
                    AuthorName = authorName,
                    PostTitle = title,
                    PostSlug = slug,
                    Embedding = embeddings[i],
                    CreatedAt = DateTime.UtcNow
                });
            }

            _context.Set<BlogChunk>().AddRange(entities);
            await _context.SaveChangesAsync();

            _logger.LogInformation("VectorRepository", $"Ingested {entities.Count} chunks for post {postId}.");
        }

        public async Task<List<BlogChunk>> SearchAsync(int postId, float[] queryEmbedding, int topK)
        {
            if (!PgVectorAvailability.IsAvailable)
                return new List<BlogChunk>();

            var queryVector = new Vector(queryEmbedding);

            var postIdParam = new NpgsqlParameter("@PostId", postId);
            var vectorParam = new NpgsqlParameter("@Vector", queryVector);
            var topKParam = new NpgsqlParameter("@TopK", topK);

            return await _context.Set<BlogChunk>()
                .FromSqlRaw(
                    "SELECT * FROM \"BlogChunks\" WHERE \"PostId\" = @PostId ORDER BY \"Embedding\" <=> @Vector LIMIT @TopK",
                    postIdParam, vectorParam, topKParam)
                .ToListAsync();
        }

        public async Task DeleteChunksAsync(int postId)
        {
            var existing = await _context.Set<BlogChunk>()
                .Where(c => c.PostId == postId)
                .ToListAsync();

            if (existing.Count > 0)
            {
                _context.Set<BlogChunk>().RemoveRange(existing);
                await _context.SaveChangesAsync();
            }
        }
    }
}
