using System.Text;
using System.Text.RegularExpressions;
using AIService.Models;

namespace AIService.Services;

public sealed class TextChunkingService : ITextChunkingService
{
    private const int DefaultMaxWords = 400;
    private const int DefaultOverlapWords = 70;

    public IReadOnlyList<TextChunk> Chunk(string text)
    {
        string[] lines = NormalizeLines(text);

        if (lines.Length == 0)
        {
            return Array.Empty<TextChunk>();
        }

        var chunks = new List<TextChunk>();
        var buffer = new List<string>(DefaultMaxWords + DefaultOverlapWords);
        string? currentSectionTitle = null;
        bool hasNewContentSinceLastChunk = false;
        int chunkIndex = 0;

        foreach (string line in lines)
        {
            bool isHeading = IsHeadingLine(line);

            if (isHeading)
            {
                if (buffer.Count >= DefaultMaxWords - DefaultOverlapWords && hasNewContentSinceLastChunk)
                {
                    AddChunk(chunks, buffer, currentSectionTitle, ref chunkIndex);
                    KeepOverlap(buffer);
                    hasNewContentSinceLastChunk = false;
                }

                currentSectionTitle = CleanSectionTitle(line);
            }
            else if (currentSectionTitle is null && LooksLikeInitialTitle(line))
            {
                currentSectionTitle = CleanSectionTitle(line);
            }

            string[] words = SplitWords(line);
            int wordOffset = 0;

            while (wordOffset < words.Length)
            {
                int remainingCapacity = DefaultMaxWords - buffer.Count;

                if (remainingCapacity <= 0)
                {
                    AddChunk(chunks, buffer, currentSectionTitle, ref chunkIndex);
                    KeepOverlap(buffer);
                    hasNewContentSinceLastChunk = false;
                    remainingCapacity = DefaultMaxWords - buffer.Count;
                }

                int take = Math.Min(remainingCapacity, words.Length - wordOffset);
                buffer.AddRange(words.Skip(wordOffset).Take(take));
                wordOffset += take;
                hasNewContentSinceLastChunk = true;
            }
        }

        if (hasNewContentSinceLastChunk && buffer.Count > 0)
        {
            AddChunk(chunks, buffer, currentSectionTitle, ref chunkIndex);
        }

        return chunks;
    }

    private static void AddChunk(
        ICollection<TextChunk> chunks,
        IReadOnlyList<string> words,
        string? sectionTitle,
        ref int chunkIndex)
    {
        string content = string.Join(' ', words).Trim();

        if (content.Length == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(sectionTitle) &&
            !NormalizeForCompare(content).Contains(NormalizeForCompare(sectionTitle), StringComparison.OrdinalIgnoreCase))
        {
            content = $"{sectionTitle}{Environment.NewLine}{content}";
        }

        chunks.Add(new TextChunk(
            chunkIndex,
            content,
            EstimateTokenCount(content),
            sectionTitle));
        chunkIndex++;
    }

    private static void KeepOverlap(List<string> words)
    {
        if (words.Count <= DefaultOverlapWords)
        {
            return;
        }

        string[] overlap = words
            .Skip(Math.Max(0, words.Count - DefaultOverlapWords))
            .ToArray();
        words.Clear();
        words.AddRange(overlap);
    }

    private static string[] NormalizeLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => line.Length > 0)
            .ToArray();
    }

    private static string[] SplitWords(string line)
    {
        return Regex
            .Split(line.Trim(), @"\s+")
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .ToArray();
    }

    private static bool IsHeadingLine(string line)
    {
        if (line.Length > 180)
        {
            return false;
        }

        if (Regex.IsMatch(
                line,
                @"^(?:chapter|chương|chuong)\s+\d+\s*[-–:]",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"^\d+[\.\)]\s+.{3,}$") && line.Length <= 120)
        {
            return true;
        }

        if (line.EndsWith(':') && CountWords(line) <= 14)
        {
            return true;
        }

        return CountWords(line) <= 18 && UppercaseLetterRatio(line) >= 0.55;
    }

    private static bool LooksLikeInitialTitle(string line)
    {
        return line.Length <= 180 &&
               CountWords(line) is >= 2 and <= 24 &&
               !line.EndsWith('.') &&
               !Regex.IsMatch(line, @"^\s*[-•]");
    }

    private static string CleanSectionTitle(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static int CountWords(string value)
    {
        return SplitWords(value).Length;
    }

    private static double UppercaseLetterRatio(string value)
    {
        int letterCount = 0;
        int uppercaseCount = 0;

        foreach (char character in value)
        {
            if (!char.IsLetter(character))
            {
                continue;
            }

            letterCount++;

            if (char.IsUpper(character))
            {
                uppercaseCount++;
            }
        }

        return letterCount == 0 ? 0 : (double)uppercaseCount / letterCount;
    }

    private static string NormalizeForCompare(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (char character in value)
        {
            builder.Append(char.IsWhiteSpace(character) ? ' ' : character);
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    private static int EstimateTokenCount(string text)
    {
        int wordCount = Regex
            .Split(text.Trim(), @"\s+")
            .Count(word => !string.IsNullOrWhiteSpace(word));

        return Math.Max(1, (int)Math.Ceiling(wordCount * 1.35));
    }
}
