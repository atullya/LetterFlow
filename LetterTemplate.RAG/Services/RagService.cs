using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LetterTemplate.RAG.Models;
using LetterTemplate.RAG.Repositories;
using Logging;
using Microsoft.Extensions.Configuration;

namespace LetterTemplate.RAG.Services
{
    public class RagService : IRagService
    {
        private readonly ChunkingService _chunkingService;
        private readonly EmbeddingService _embeddingService;
        private readonly VectorRepository _vectorRepository;
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _chatModel;
        private readonly IAppLogger _logger;
        private readonly int _topK;

        public RagService(
            ChunkingService chunkingService,
            EmbeddingService embeddingService,
            VectorRepository vectorRepository,
            IHttpClientFactory factory,
            IConfiguration config,
            IAppLogger logger)
        {
            _chunkingService = chunkingService;
            _embeddingService = embeddingService;
            _vectorRepository = vectorRepository;
            _http = factory.CreateClient("gemini");
            _apiKey = config["Gemini:ApiKey"]
                      ?? throw new InvalidOperationException("Gemini:ApiKey is not configured.");
            _chatModel = config["Gemini:ChatModel"] ?? "gemini-2.5-flash";
            _logger = logger;
            _topK = config.GetValue<int>("Rag:TopK");
            if (_topK == 0) _topK = 5;
        }

        public async Task<RagResult> AskAsync(RagQuery query)
        {
            if (!PgVectorAvailability.IsAvailable)
            {
                return new RagResult
                {
                    Success = false,
                    ErrorMessage = "RAG features are not available — pgvector extension is not installed on the database.",
                    Answer = "Sorry, the AI-powered Q&A feature is currently unavailable because the vector database extension (pgvector) has not been set up yet."
                };
            }

            if (string.IsNullOrWhiteSpace(query.Question))
            {
                return new RagResult { Success = false, ErrorMessage = "Question is empty.", Answer = "Please ask a question." };
            }

            try
            {
                var questionEmbedding = await _embeddingService.EmbedAsync(query.Question);

                var chunks = await _vectorRepository.SearchAsync(
                    query.PostId, questionEmbedding, query.TopK > 0 ? query.TopK : _topK);

                if (chunks.Count == 0)
                {
                    return new RagResult
                    {
                        Success = false,
                        ErrorMessage = "No content chunks found for this post. The post may not have been indexed yet.",
                        Answer = "I couldn't find any indexed content for this blog post. This might be a newly created post that hasn't been processed yet."
                    };
                }

                var contextBuilder = new StringBuilder();
                contextBuilder.AppendLine("Here is the blog post content you should answer from:");
                foreach (var chunk in chunks)
                {
                    contextBuilder.AppendLine($"--- From '{chunk.PostTitle}' by {chunk.AuthorName} ---");
                    contextBuilder.AppendLine(chunk.Content);
                    contextBuilder.AppendLine();
                }

                var systemPrompt =
                    "You are a helpful assistant that only answers questions based on the provided blog post content. " +
                    "If the answer is not in the content, say you don't know. " +
                    "Never make up information. Always cite which part of the blog you got the answer from.";

                var userContent = $"{contextBuilder}\n\nQuestion: {query.Question}";

                var answer = await CallGeminiAsync(systemPrompt, userContent);

                return new RagResult
                {
                    Success = true,
                    Answer = answer,
                    SourceChunks = chunks.Select(c => c.Content).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("RagService", $"AskAsync failed for post {query.PostId}", ex);
                return new RagResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task IngestPostAsync(int postId, string htmlContent, string title, string authorName, string slug)
        {
            if (!PgVectorAvailability.IsAvailable)
            {
                _logger.LogWarning("RagService", $"Skipping ingest for post {postId} — pgvector not available.");
                return;
            }

            try
            {
                var chunks = _chunkingService.ChunkContent(htmlContent);

                if (chunks.Count == 0)
                {
                    _logger.LogWarning("RagService", $"No chunks generated for post {postId}.");
                    return;
                }

                _logger.LogInformation("RagService", $"Chunked post {postId} into {chunks.Count} chunks. Embedding...");

                var embeddings = await _embeddingService.EmbedBatchAsync(chunks);

                await _vectorRepository.IngestChunksAsync(postId, chunks, embeddings, title, authorName, slug);

                _logger.LogInformation("RagService", $"Successfully ingested post {postId}.");
            }
            catch (Exception ex)
            {
                _logger.LogError("RagService", $"IngestPostAsync failed for post {postId}", ex);
            }
        }

        private async Task<string> CallGeminiAsync(string systemPrompt, string userContent)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_chatModel}:generateContent?key={_apiKey}";

            var body = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userContent } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 1024
                }
            };

            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var response = await _http.PostAsJsonAsync(url, body);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadFromJsonAsync<GeminiChatResponse>();
                    return json?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim()
                           ?? "No response from AI.";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("RagService", $"Gemini chat attempt {attempt + 1}/3 failed: {ex.Message}");
                    if (attempt == 2)
                        throw;

                    var delay = (int)Math.Pow(2, attempt) * 1000;
                    await Task.Delay(delay);
                }
            }

            throw new InvalidOperationException("Gemini chat failed after all retries.");
        }

        private sealed class GeminiChatResponse
        {
            [JsonPropertyName("candidates")]
            public List<Candidate>? Candidates { get; set; }
        }

        private sealed class Candidate
        {
            [JsonPropertyName("content")]
            public Content? Content { get; set; }
        }

        private sealed class Content
        {
            [JsonPropertyName("parts")]
            public List<Part>? Parts { get; set; }
        }

        private sealed class Part
        {
            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }
    }
}
