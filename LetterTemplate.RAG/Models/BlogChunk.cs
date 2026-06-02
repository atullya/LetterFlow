using System.ComponentModel.DataAnnotations;

namespace LetterTemplate.RAG.Models
{
    public class BlogChunk
    {
        public int Id { get; set; }

        public int PostId { get; set; }

        [MaxLength(10000)]
        public string Content { get; set; } = string.Empty;

        public int ChunkIndex { get; set; }

        [MaxLength(200)]
        public string AuthorName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string PostTitle { get; set; } = string.Empty;

        [MaxLength(200)]
        public string PostSlug { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public float[]? Embedding { get; set; }
    }
}
