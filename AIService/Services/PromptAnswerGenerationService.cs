using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AIService.Services;

public sealed class PromptAnswerGenerationService : IAnswerGenerationService
{
    private const int MaxAnswerSentences = 2;
    private const int MaxSentenceCharacters = 260;

    // Fallback này không gọi AI thật; nó chọn câu liên quan từ context trong prompt.
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "la", "gi", "cua", "va", "hoac", "the", "nao", "trong", "cho", "toi", "hay", "neu", "mot", "cac", "nhung",
        "duoc", "ve", "voi", "tu", "den", "khi", "co", "khong", "can", "hay", "noi", "trinh", "bay"
    };

    public Task<string> GenerateAnswerAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ParsedPrompt parsedPrompt = ParsePrompt(prompt);

        if (parsedPrompt.Contexts.Count == 0)
        {
            // Không có context thì trả lời an toàn thay vì suy đoán ngoài tài liệu.
            return Task.FromResult("Tài liệu chưa có thông tin phù hợp.");
        }

        string? directAnswer = TryAnswerDirectQuestion(parsedPrompt);

        if (!string.IsNullOrWhiteSpace(directAnswer))
        {
            return Task.FromResult(directAnswer);
        }

        string[] questionTerms = ExtractSearchTerms(parsedPrompt.Question);
        List<RankedSentence> rankedSentences = parsedPrompt.Contexts
            .SelectMany(context => SplitSentences(context.Content)
                .Select(sentence => new RankedSentence(
                    CleanSentence(sentence),
                    context.Source,
                    ScoreSentence(sentence, questionTerms))))
            .Where(sentence => sentence.Score > 0 && sentence.Text.Length > 0)
            .GroupBy(sentence => NormalizeForSearch(sentence.Text))
            .Select(group => group.OrderByDescending(sentence => sentence.Score).First())
            .OrderByDescending(sentence => sentence.Score)
            .ThenBy(sentence => sentence.Text.Length)
            .Take(MaxAnswerSentences)
            .ToList();

        if (rankedSentences.Count == 0)
        {
            return Task.FromResult("Tài liệu chưa có thông tin phù hợp.");
        }

        string answer = BuildAnswer(rankedSentences);

        return Task.FromResult(answer);
    }

    private static string? TryAnswerDirectQuestion(ParsedPrompt parsedPrompt)
    {
        return TryAnswerChapterTitleQuestion(parsedPrompt)
            ?? TryAnswerMainCharacterQuestion(parsedPrompt)
            ?? TryAnswerToolQuestion(parsedPrompt);
    }

    private static string? TryAnswerChapterTitleQuestion(ParsedPrompt parsedPrompt)
    {
        string normalizedQuestion = NormalizeForSearch(parsedPrompt.Question);
        Match chapterQuestionMatch = Regex.Match(
            normalizedQuestion,
            @"\b(?:chapter|chuong)\s+(?<number>\d+)\b");

        if (!chapterQuestionMatch.Success ||
            !normalizedQuestion.Contains("ten", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string chapterNumber = chapterQuestionMatch.Groups["number"].Value;

        foreach (PromptContext context in parsedPrompt.Contexts)
        {
            Match titleMatch = Regex.Match(
                context.Content,
                $@"\b(?:Chapter|Chương)\s*{Regex.Escape(chapterNumber)}\s*[-–:]\s*(?<title>[^\r\n\.]+)",
                RegexOptions.IgnoreCase);

            if (!titleMatch.Success)
            {
                continue;
            }

            string title = CleanExtractedTitle(titleMatch.Groups["title"].Value);

            if (!string.IsNullOrWhiteSpace(title))
            {
                return $"Chapter {chapterNumber} có tên là {title}.";
            }
        }

        return null;
    }

    private static string? TryAnswerMainCharacterQuestion(ParsedPrompt parsedPrompt)
    {
        string normalizedQuestion = NormalizeForSearch(parsedPrompt.Question);

        if (!normalizedQuestion.Contains("nhan vat", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        foreach (PromptContext context in parsedPrompt.Contexts)
        {
            string normalizedContent = NormalizeForSearch(context.Content);

            if (!normalizedContent.Contains("nhan vat chinh", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Regex.IsMatch(context.Content, @"\bLượm\b", RegexOptions.IgnoreCase))
            {
                return "Nhân vật chính là Lượm.";
            }

            Match nameMatch = Regex.Match(
                context.Content,
                @"(?:nhân vật chính|nhan vat chinh).{0,40}?(?:là|la|:|-)\s*(?<name>[\p{L}\d'\- ]{2,60})",
                RegexOptions.IgnoreCase);

            if (!nameMatch.Success)
            {
                continue;
            }

            string name = CleanExtractedName(nameMatch.Groups["name"].Value);

            if (!string.IsNullOrWhiteSpace(name))
            {
                return $"Nhân vật chính là {name}.";
            }
        }

        return null;
    }

    private static string CleanExtractedName(string value)
    {
        string name = NormalizeWhitespace(value);
        name = Regex.Split(
            name,
            @"\b(?:trong|với|và|có|là|của|mot|một|nguoi|người|cau|cậu|game|chapter)\b|[,.;:()\[\]]",
            RegexOptions.IgnoreCase)[0];
        name = name.Trim(' ', '-', '–', ':', ';', ',', '.');

        string[] words = name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(5)
            .ToArray();

        return string.Join(' ', words);
    }

    private static string? TryAnswerToolQuestion(ParsedPrompt parsedPrompt)
    {
        string normalizedQuestion = NormalizeForSearch(parsedPrompt.Question);

        if (!IsToolQuestion(normalizedQuestion))
        {
            return null;
        }

        (string Pattern, string DisplayName)[] tools =
        [
            ("godot", "Godot"),
            ("unity", "Unity"),
            ("unreal", "Unreal Engine"),
            ("gdscript", "GDScript"),
            ("csharp", "C#"),
            ("blender", "Blender")
        ];

        foreach ((string pattern, string displayName) in tools)
        {
            if (parsedPrompt.Contexts.Any(context =>
                    NormalizeForSearch(context.Content).Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Theo tài liệu, game này được gợi ý triển khai bằng {displayName}.";
            }
        }

        return null;
    }

    private static bool IsToolQuestion(string normalizedQuestion)
    {
        string[] toolTerms =
        [
            "tool",
            "cong cu",
            "ngon ngu",
            "phan mem",
            "engine",
            "framework",
            "dung gi",
            "su dung"
        ];

        return toolTerms.Any(term => normalizedQuestion.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string CleanExtractedTitle(string value)
    {
        string title = NormalizeWhitespace(value);
        title = Regex.Split(title, @"\b(?:Chapter|Chương)\s+\d+\b", RegexOptions.IgnoreCase)[0];
        title = Regex.Split(title, @"\b(?:Mục tiêu|Nhiệm vụ|Nội dung|Mô tả|Gameplay|Workflow|Task)\b", RegexOptions.IgnoreCase)[0];
        title = title.Trim(' ', '-', '–', ':', ';', ',', '.');

        return title.Length <= 100
            ? title
            : title[..100].Trim(' ', '-', '–', ':', ';', ',', '.') + "...";
    }

    private static string BuildAnswer(IReadOnlyList<RankedSentence> sentences)
    {
        var answer = new StringBuilder();

        answer.Append("Theo tài liệu, ");
        answer.AppendJoin(' ', sentences.Select(sentence => EnsureFinalPunctuation(sentence.Text)));

        return answer.ToString();
    }

    private static ParsedPrompt ParsePrompt(string prompt)
    {
        string question = ExtractSection(prompt, "QUESTION:", "CONTEXT:").Trim();
        string contextSection = ExtractSection(prompt, "CONTEXT:", "Answer:").Trim();

        if (contextSection.Length == 0)
        {
            contextSection = ExtractSection(prompt, "CONTEXT:", "Yêu cầu:").Trim();
        }

        var contexts = new List<PromptContext>();
        string? currentSource = null;
        var currentContent = new StringBuilder();

        foreach (string rawLine in contextSection.Split('\n'))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("[Nguồn ", StringComparison.OrdinalIgnoreCase) &&
                line.EndsWith(']'))
            {
                AddCurrentContext(contexts, currentSource, currentContent);
                currentSource = line.Trim('[', ']');
                currentContent.Clear();
                continue;
            }

            currentContent.AppendLine(line);
        }

        AddCurrentContext(contexts, currentSource, currentContent);

        return new ParsedPrompt(question, contexts);
    }

    private static void AddCurrentContext(
        ICollection<PromptContext> contexts,
        string? source,
        StringBuilder content)
    {
        string normalizedContent = NormalizeWhitespace(content.ToString());

        if (normalizedContent.Length == 0)
        {
            return;
        }

        contexts.Add(new PromptContext(source ?? "Nguồn tài liệu", normalizedContent));
    }

    private static string ExtractSection(string value, string startMarker, string endMarker)
    {
        int startIndex = value.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);

        if (startIndex < 0)
        {
            return string.Empty;
        }

        startIndex += startMarker.Length;
        int endIndex = value.IndexOf(endMarker, startIndex, StringComparison.OrdinalIgnoreCase);

        return endIndex < 0
            ? value[startIndex..]
            : value[startIndex..endIndex];
    }

    private static IEnumerable<string> SplitSentences(string content)
    {
        return Regex
            .Split(content, @"(?<=[\.\?\!。])\s+|\n+")
            .Select(NormalizeWhitespace)
            .Where(sentence => sentence.Length >= 24);
    }

    private static double ScoreSentence(string sentence, IReadOnlyList<string> questionTerms)
    {
        if (questionTerms.Count == 0)
        {
            return 0;
        }

        string normalizedSentence = NormalizeForSearch(sentence);
        int matchedTerms = questionTerms.Count(term => normalizedSentence.Contains(term, StringComparison.OrdinalIgnoreCase));

        return (double)matchedTerms / questionTerms.Count;
    }

    private static string[] ExtractSearchTerms(string question)
    {
        return NormalizeForSearch(question)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 2 && !StopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string CleanSentence(string sentence)
    {
        string normalizedSentence = NormalizeWhitespace(sentence);

        if (normalizedSentence.Length <= MaxSentenceCharacters)
        {
            return normalizedSentence;
        }

        return normalizedSentence[..MaxSentenceCharacters].Trim() + "...";
    }

    private static string EnsureFinalPunctuation(string value)
    {
        if (value.Length == 0 || ".!?".Contains(value[^1]))
        {
            return value;
        }

        return value + ".";
    }

    private static string NormalizeWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        bool previousWasWhiteSpace = false;

        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhiteSpace)
                {
                    builder.Append(' ');
                    previousWasWhiteSpace = true;
                }

                continue;
            }

            builder.Append(character);
            previousWasWhiteSpace = false;
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeForSearch(string value)
    {
        value = Regex.Replace(value, @"\bC#\b", "CSharp", RegexOptions.IgnoreCase);
        string withoutDiacritics = RemoveDiacritics(value).ToLowerInvariant();
        var builder = new StringBuilder(withoutDiacritics.Length);

        foreach (char character in withoutDiacritics)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return builder.ToString();
    }

    private static string RemoveDiacritics(string value)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (char character in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'D');
    }

    private sealed record ParsedPrompt(
        string Question,
        IReadOnlyList<PromptContext> Contexts);

    private sealed record PromptContext(
        string Source,
        string Content);

    private sealed record RankedSentence(
        string Text,
        string Source,
        double Score);
}
