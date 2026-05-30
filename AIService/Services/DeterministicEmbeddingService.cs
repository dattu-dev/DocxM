using System.Security.Cryptography;
using System.Text;
using AIService.Models;

namespace AIService.Services;

public sealed class DeterministicEmbeddingService : IEmbeddingService
{
    private const int Dimension = 64;

    public Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return GenerateEmbeddingAsync(text, EmbeddingTaskType.RetrievalDocument, cancellationToken);
    }

    public Task<float[]> GenerateEmbeddingAsync(
        string text,
        EmbeddingTaskType taskType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] input = Encoding.UTF8.GetBytes(text ?? string.Empty);
        byte[] hash = SHA256.HashData(input);
        var vector = new float[Dimension];

        for (int i = 0; i < vector.Length; i++)
        {
            byte value = hash[i % hash.Length];
            vector[i] = (value - 127.5f) / 127.5f;
        }

        Normalize(vector);

        return Task.FromResult(vector);
    }

    private static void Normalize(float[] vector)
    {
        double sumSquares = 0;

        foreach (float value in vector)
        {
            sumSquares += value * value;
        }

        double magnitude = Math.Sqrt(sumSquares);

        if (magnitude <= 0)
        {
            return;
        }

        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / magnitude);
        }
    }
}
