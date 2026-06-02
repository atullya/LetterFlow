namespace LetterTemplate.RAG.Models
{
    public class RagResult
    {
        public string Answer { get; set; } = string.Empty;
        public List<string> SourceChunks { get; set; } = new();
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
