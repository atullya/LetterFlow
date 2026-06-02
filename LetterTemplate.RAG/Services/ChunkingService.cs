using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace LetterTemplate.RAG.Services
{
    public class ChunkingService
    {
        private readonly int _minChunkSize;
        private readonly int _maxChunkSize;

        public ChunkingService(int minChunkSize = 100, int maxChunkSize = 500)
        {
            _minChunkSize = minChunkSize;
            _maxChunkSize = maxChunkSize;
        }

        public List<string> ChunkContent(string htmlContent)
        {
            var plainText = StripHtml(htmlContent);

            var paragraphs = SplitParagraphs(plainText);

            var chunks = NormalizeChunks(paragraphs);

            return chunks;
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return doc.DocumentNode.InnerText;
        }

        private List<string> SplitParagraphs(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            var normalized = Regex.Replace(text, @"\r\n|\r", "\n");

            var parts = normalized.Split(new[] { "\n\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            return parts
                .Select(p => Regex.Replace(p.Trim(), @"\s+", " "))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
        }

        private List<string> NormalizeChunks(List<string> paragraphs)
        {
            var raw = new List<string>();
            var buffer = string.Empty;

            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(buffer))
                {
                    buffer = para;
                }
                else if (buffer.Length + para.Length + 1 <= _maxChunkSize)
                {
                    buffer += " " + para;
                }
                else
                {
                    if (buffer.Length >= _minChunkSize)
                    {
                        raw.Add(buffer.Trim());
                        buffer = para;
                    }
                    else
                    {
                        buffer += " " + para;
                        if (buffer.Length >= _minChunkSize)
                        {
                            raw.Add(buffer.Trim());
                            buffer = string.Empty;
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(buffer) && buffer.Length >= 10)
            {
                raw.Add(buffer.Trim());
            }

            var processed = new List<string>();
            foreach (var chunk in raw)
            {
                if (chunk.Length <= _maxChunkSize)
                {
                    if (chunk.Length >= 10)
                        processed.Add(chunk);
                }
                else
                {
                    var subChunks = SplitLongChunk(chunk);
                    processed.AddRange(subChunks.Where(c => c.Length >= 10));
                }
            }

            return processed;
        }

        private List<string> SplitLongChunk(string text)
        {
            var results = new List<string>();
            var sentences = Regex.Split(text, @"(?<=[.!?])\s+");
            var buffer = string.Empty;

            foreach (var sentence in sentences)
            {
                var trimmed = sentence.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                if (string.IsNullOrWhiteSpace(buffer))
                {
                    buffer = trimmed;
                }
                else if (buffer.Length + trimmed.Length + 1 <= _maxChunkSize)
                {
                    buffer += " " + trimmed;
                }
                else
                {
                    if (buffer.Length >= _minChunkSize || string.IsNullOrWhiteSpace(buffer) == false)
                    {
                        results.Add(buffer.Trim());
                    }
                    buffer = trimmed;
                }
            }

            if (!string.IsNullOrWhiteSpace(buffer))
            {
                results.Add(buffer.Trim());
            }

            if (results.Count == 0)
            {
                for (var i = 0; i < text.Length; i += _maxChunkSize)
                {
                    var chunk = text.Substring(i, Math.Min(_maxChunkSize, text.Length - i)).Trim();
                    if (chunk.Length >= 10)
                        results.Add(chunk);
                }
            }

            return results;
        }
    }
}
