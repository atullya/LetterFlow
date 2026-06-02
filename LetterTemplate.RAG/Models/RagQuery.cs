namespace LetterTemplate.RAG.Models
{
    public class RagQuery
    {
        public int PostId { get; set; }
        public string Question { get; set; } = string.Empty;
        public int TopK { get; set; } = 5;
    }
}
