IF COL_LENGTH(N'dbo.DocumentChunks', N'SectionTitle') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentChunks ADD SectionTitle NVARCHAR(250) NULL;
END
GO

IF OBJECT_ID(N'dbo.EmbeddingModels', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM dbo.EmbeddingModels
        WHERE Provider = N'Gemini'
          AND Name = N'gemini-embedding-001'
   )
BEGIN
    INSERT INTO dbo.EmbeddingModels (Provider, Name, Dimension)
    VALUES (N'Gemini', N'gemini-embedding-001', 3072);
END
GO