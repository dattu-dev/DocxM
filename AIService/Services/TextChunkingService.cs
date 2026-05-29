using System.Text.RegularExpressions;
using AIService.Models;

namespace AIService.Services;

public sealed class TextChunkingService : ITextChunkingService
{
    private const int DefaultMaxWords = 800;
    private const int DefaultOverlapWords = 120;

    public IReadOnlyList<TextChunk> Chunk(string text)
    {
        string[] words = Regex
            .Split(text.Trim(), @"\s+")
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .ToArray();

        if (words.Length == 0)
        {
            return Array.Empty<TextChunk>();
        }

        var chunks = new List<TextChunk>();
        int step = Math.Max(1, DefaultMaxWords - DefaultOverlapWords);
        int chunkIndex = 0;

        for (int start = 0; start < words.Length;)
        {
            int length = Math.Min(DefaultMaxWords, words.Length - start);
            string content = string.Join(' ', words.Skip(start).Take(length));

            chunks.Add(new TextChunk(
                chunkIndex,
                content,
                EstimateTokenCount(content)));

            if (start + length >= words.Length)
            {
                break;
            }

            start += step;
            chunkIndex++;
        }

        return chunks;
    }

    private static int EstimateTokenCount(string text)
    {
        int wordCount = Regex
            .Split(text.Trim(), @"\s+")
            .Count(word => !string.IsNullOrWhiteSpace(word));

        return Math.Max(1, (int)Math.Ceiling(wordCount * 1.35));
    }
}
