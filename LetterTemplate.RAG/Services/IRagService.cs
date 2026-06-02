using LetterTemplate.RAG.Models;

namespace LetterTemplate.RAG.Services
{
    public interface IRagService
    {
        Task<RagResult> AskAsync(RagQuery query);
        Task IngestPostAsync(int postId, string htmlContent, string title, string authorName, string slug);
    }
}
