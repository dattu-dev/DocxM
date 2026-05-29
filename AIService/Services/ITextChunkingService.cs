using AIService.Models;

namespace AIService.Services;

public interface ITextChunkingService
{
    IReadOnlyList<TextChunk> Chunk(string text);
}
