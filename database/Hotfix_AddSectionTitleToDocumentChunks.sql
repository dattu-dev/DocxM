IF COL_LENGTH(N'dbo.DocumentChunks', N'SectionTitle') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentChunks ADD SectionTitle NVARCHAR(250) NULL;
END
GO
