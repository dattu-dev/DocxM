USE master;
GO

IF DB_ID(N'DocXM') IS NOT NULL
BEGIN
    ALTER DATABASE DocXM SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE DocXM;
END
GO

CREATE DATABASE DocXM;
GO

USE DocXM;
GO

CREATE TABLE dbo.AppUsers
(
    UserId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUsers PRIMARY KEY,
    UserName NVARCHAR(100) NOT NULL,
    NormalizedUserName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(256) NOT NULL,
    NormalizedEmail NVARCHAR(256) NOT NULL,
    FullName NVARCHAR(150) NOT NULL,
    PasswordHash NVARCHAR(500) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_AppUsers_IsActive DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_AppUsers_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

CREATE UNIQUE INDEX UX_AppUsers_NormalizedUserName ON dbo.AppUsers(NormalizedUserName);
CREATE UNIQUE INDEX UX_AppUsers_NormalizedEmail ON dbo.AppUsers(NormalizedEmail);
GO

CREATE TABLE dbo.Subjects
(
    SubjectId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Subjects PRIMARY KEY,
    CreatedByUserId INT NOT NULL,
    Code NVARCHAR(50) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Subjects_IsActive DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Subjects_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Subjects_AppUsers FOREIGN KEY (CreatedByUserId)
        REFERENCES dbo.AppUsers(UserId)
);
GO

CREATE UNIQUE INDEX UX_Subjects_Code ON dbo.Subjects(Code);
CREATE INDEX IX_Subjects_CreatedByUserId ON dbo.Subjects(CreatedByUserId);
GO

CREATE TABLE dbo.Chapters
(
    ChapterId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Chapters PRIMARY KEY,
    SubjectId INT NOT NULL,
    Title NVARCHAR(250) NOT NULL,
    ChapterNumber INT NOT NULL,
    Description NVARCHAR(1000) NULL,
    SortOrder INT NOT NULL CONSTRAINT DF_Chapters_SortOrder DEFAULT (0),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Chapters_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Chapters_Subjects FOREIGN KEY (SubjectId)
        REFERENCES dbo.Subjects(SubjectId)
);
GO

CREATE INDEX IX_Chapters_SubjectId ON dbo.Chapters(SubjectId);
CREATE UNIQUE INDEX UX_Chapters_SubjectId_ChapterNumber ON dbo.Chapters(SubjectId, ChapterNumber);
GO

CREATE TABLE dbo.Documents
(
    DocumentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Documents PRIMARY KEY,
    UploadedByUserId INT NOT NULL,
    SubjectId INT NOT NULL,
    ChapterId INT NOT NULL,
    Title NVARCHAR(250) NOT NULL,
    Description NVARCHAR(1000) NULL,
    OriginalFileName NVARCHAR(260) NOT NULL,
    StoredFileName NVARCHAR(260) NOT NULL,
    StoragePath NVARCHAR(500) NOT NULL,
    ContentType NVARCHAR(150) NOT NULL,
    FileExtension NVARCHAR(20) NOT NULL,
    FileSizeBytes BIGINT NOT NULL,
    ProcessingStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_Documents_ProcessingStatus DEFAULT (N'Uploaded'),
    ChunkCount INT NOT NULL CONSTRAINT DF_Documents_ChunkCount DEFAULT (0),
    UploadedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Documents_UploadedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    CONSTRAINT FK_Documents_AppUsers FOREIGN KEY (UploadedByUserId)
        REFERENCES dbo.AppUsers(UserId),
    CONSTRAINT FK_Documents_Subjects FOREIGN KEY (SubjectId)
        REFERENCES dbo.Subjects(SubjectId),
    CONSTRAINT FK_Documents_Chapters FOREIGN KEY (ChapterId)
        REFERENCES dbo.Chapters(ChapterId)
);
GO

CREATE INDEX IX_Documents_UploadedByUserId ON dbo.Documents(UploadedByUserId);
CREATE INDEX IX_Documents_SubjectId ON dbo.Documents(SubjectId);
CREATE INDEX IX_Documents_ChapterId ON dbo.Documents(ChapterId);
CREATE INDEX IX_Documents_ProcessingStatus ON dbo.Documents(ProcessingStatus);
GO

CREATE TABLE dbo.DocumentChunks
(
    DocumentChunkId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocumentChunks PRIMARY KEY,
    DocumentId INT NOT NULL,
    ChunkIndex INT NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    TokenCount INT NULL,
    PageNumber INT NULL,
    SectionTitle NVARCHAR(250) NULL,
    VectorId NVARCHAR(100) NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_DocumentChunks_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_DocumentChunks_Documents FOREIGN KEY (DocumentId)
        REFERENCES dbo.Documents(DocumentId)
        ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX UX_DocumentChunks_DocumentId_ChunkIndex ON dbo.DocumentChunks(DocumentId, ChunkIndex);
GO

CREATE TABLE dbo.EmbeddingModels
(
    EmbeddingModelId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmbeddingModels PRIMARY KEY,
    Provider NVARCHAR(100) NOT NULL,
    Name NVARCHAR(150) NOT NULL,
    Dimension INT NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_EmbeddingModels_IsActive DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_EmbeddingModels_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

CREATE UNIQUE INDEX UX_EmbeddingModels_Provider_Name ON dbo.EmbeddingModels(Provider, Name);
GO

CREATE TABLE dbo.DocumentEmbeddings
(
    DocumentEmbeddingId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocumentEmbeddings PRIMARY KEY,
    DocumentChunkId BIGINT NOT NULL,
    EmbeddingModelId INT NOT NULL,
    VectorJson NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_DocumentEmbeddings_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_DocumentEmbeddings_DocumentChunks FOREIGN KEY (DocumentChunkId)
        REFERENCES dbo.DocumentChunks(DocumentChunkId)
        ON DELETE CASCADE,
    CONSTRAINT FK_DocumentEmbeddings_EmbeddingModels FOREIGN KEY (EmbeddingModelId)
        REFERENCES dbo.EmbeddingModels(EmbeddingModelId)
);
GO

CREATE UNIQUE INDEX UX_DocumentEmbeddings_Chunk_Model
    ON dbo.DocumentEmbeddings(DocumentChunkId, EmbeddingModelId);
GO

CREATE TABLE dbo.ChatConversations
(
    ChatConversationId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChatConversations PRIMARY KEY,
    UserId INT NOT NULL,
    SubjectId INT NULL,
    ChapterId INT NULL,
    DocumentId INT NULL,
    Title NVARCHAR(250) NOT NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_ChatConversations_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_ChatConversations_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_ChatConversations_AppUsers FOREIGN KEY (UserId)
        REFERENCES dbo.AppUsers(UserId)
        ON DELETE CASCADE
);
GO

CREATE INDEX IX_ChatConversations_UserId ON dbo.ChatConversations(UserId);
CREATE INDEX IX_ChatConversations_Scope ON dbo.ChatConversations(UserId, SubjectId, ChapterId, DocumentId);
GO

CREATE TABLE dbo.ChatMessages
(
    ChatMessageId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChatMessages PRIMARY KEY,
    ChatConversationId INT NOT NULL,
    UserQuestion NVARCHAR(1000) NOT NULL,
    AiAnswer NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_ChatMessages_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_ChatMessages_ChatConversations FOREIGN KEY (ChatConversationId)
        REFERENCES dbo.ChatConversations(ChatConversationId)
        ON DELETE CASCADE
);
GO

CREATE INDEX IX_ChatMessages_ConversationId ON dbo.ChatMessages(ChatConversationId);
GO

CREATE TABLE dbo.ChatCitations
(
    ChatCitationId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChatCitations PRIMARY KEY,
    ChatMessageId BIGINT NOT NULL,
    DocumentId INT NOT NULL,
    DocumentChunkId BIGINT NOT NULL,
    DocumentName NVARCHAR(260) NOT NULL,
    PageNumber INT NULL,
    Snippet NVARCHAR(1000) NOT NULL,
    SimilarityScore FLOAT NOT NULL,
    CONSTRAINT FK_ChatCitations_ChatMessages FOREIGN KEY (ChatMessageId)
        REFERENCES dbo.ChatMessages(ChatMessageId)
        ON DELETE CASCADE
);
GO

CREATE INDEX IX_ChatCitations_MessageId ON dbo.ChatCitations(ChatMessageId);
CREATE INDEX IX_ChatCitations_DocumentId ON dbo.ChatCitations(DocumentId);
GO

INSERT INTO dbo.EmbeddingModels (Provider, Name, Dimension)
VALUES
    (N'Local', N'deterministic-demo-64', 64),
    (N'OpenAI', N'text-embedding-3-small', 1536),
    (N'BAAI', N'bge-m3', 1024),
    (N'Gemini', N'gemini-embedding-001', 3072);
GO
