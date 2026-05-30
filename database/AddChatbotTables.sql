USE DocXM;
GO

IF OBJECT_ID(N'dbo.ChatConversations', N'U') IS NULL
BEGIN
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
END
GO

IF OBJECT_ID(N'dbo.ChatConversations', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatConversations_UserId' AND object_id = OBJECT_ID(N'dbo.ChatConversations'))
BEGIN
    CREATE INDEX IX_ChatConversations_UserId ON dbo.ChatConversations(UserId);
END
GO

IF OBJECT_ID(N'dbo.ChatConversations', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatConversations_Scope' AND object_id = OBJECT_ID(N'dbo.ChatConversations'))
BEGIN
    CREATE INDEX IX_ChatConversations_Scope ON dbo.ChatConversations(UserId, SubjectId, ChapterId, DocumentId);
END
GO

IF OBJECT_ID(N'dbo.ChatMessages', N'U') IS NULL
BEGIN
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
END
GO

IF OBJECT_ID(N'dbo.ChatMessages', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatMessages_ConversationId' AND object_id = OBJECT_ID(N'dbo.ChatMessages'))
BEGIN
    CREATE INDEX IX_ChatMessages_ConversationId ON dbo.ChatMessages(ChatConversationId);
END
GO

IF OBJECT_ID(N'dbo.ChatCitations', N'U') IS NULL
BEGIN
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
END
GO

IF OBJECT_ID(N'dbo.ChatCitations', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatCitations_MessageId' AND object_id = OBJECT_ID(N'dbo.ChatCitations'))
BEGIN
    CREATE INDEX IX_ChatCitations_MessageId ON dbo.ChatCitations(ChatMessageId);
END
GO

IF OBJECT_ID(N'dbo.ChatCitations', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatCitations_DocumentId' AND object_id = OBJECT_ID(N'dbo.ChatCitations'))
BEGIN
    CREATE INDEX IX_ChatCitations_DocumentId ON dbo.ChatCitations(DocumentId);
END
GO
