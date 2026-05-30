using System.Text.Json;
using AIService.Models;

namespace AIService.Services;

public sealed class LocalJsonVectorStore : IVectorStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public async Task UpsertAsync(
        VectorRecord vector,
        string storageRootPath,
        CancellationToken cancellationToken = default)
    {
        string storePath = GetStorePath(storageRootPath);

        await FileLock.WaitAsync(cancellationToken);

        try
        {
            List<VectorRecord> vectors = await ReadVectorsAsync(storePath, cancellationToken);
            vectors.RemoveAll(item => item.VectorId == vector.VectorId);
            vectors.Add(vector);

            Directory.CreateDirectory(Path.GetDirectoryName(storePath)!);

            await using FileStream stream = File.Create(storePath);
            await JsonSerializer.SerializeAsync(stream, vectors, SerializerOptions, cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryVector,
        string storageRootPath,
        IReadOnlyCollection<int> documentIds,
        int topK,
        CancellationToken cancellationToken = default)
    {
        if (topK <= 0 || queryVector.Length == 0 || documentIds.Count == 0)
        {
            return Array.Empty<VectorSearchResult>();
        }

        string storePath = GetStorePath(storageRootPath);

        await FileLock.WaitAsync(cancellationToken);

        try
        {
            HashSet<int> allowedDocumentIds = documentIds.ToHashSet();
            List<VectorRecord> vectors = await ReadVectorsAsync(storePath, cancellationToken);

            return vectors
                .Where(vector => allowedDocumentIds.Contains(vector.DocumentId))
                .Select(vector => new VectorSearchResult(vector, CosineSimilarity(queryVector, vector.EmbeddingVector)))
                .OrderByDescending(result => result.Score)
                .Take(topK)
                .ToList();
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task DeleteByDocumentIdAsync(
        int documentId,
        string storageRootPath,
        CancellationToken cancellationToken = default)
    {
        string storePath = GetStorePath(storageRootPath);

        await FileLock.WaitAsync(cancellationToken);

        try
        {
            List<VectorRecord> vectors = await ReadVectorsAsync(storePath, cancellationToken);
            int removed = vectors.RemoveAll(item => item.DocumentId == documentId);

            if (removed == 0)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(storePath)!);

            await using FileStream stream = File.Create(storePath);
            await JsonSerializer.SerializeAsync(stream, vectors, SerializerOptions, cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private static async Task<List<VectorRecord>> ReadVectorsAsync(
        string storePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(storePath))
        {
            return new List<VectorRecord>();
        }

        await using FileStream stream = File.OpenRead(storePath);

        return await JsonSerializer.DeserializeAsync<List<VectorRecord>>(
            stream,
            SerializerOptions,
            cancellationToken) ?? new List<VectorRecord>();
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        int length = Math.Min(left.Length, right.Length);

        if (length == 0)
        {
            return 0;
        }

        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (int i = 0; i < length; i++)
        {
            dot += left[i] * right[i];
            leftMagnitude += left[i] * left[i];
            rightMagnitude += right[i] * right[i];
        }

        if (leftMagnitude <= 0 || rightMagnitude <= 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private static string GetStorePath(string storageRootPath)
    {
        string root = Path.GetFullPath(storageRootPath);

        if (string.Equals(Path.GetFileName(root), "wwwroot", StringComparison.OrdinalIgnoreCase))
        {
            root = Directory.GetParent(root)?.FullName ?? root;
        }

        return Path.Combine(root, "App_Data", "vector-store", "vectors.json");
    }
}
