using AIService.Models;
using AIService.Services;
using BusinessLogic.DTOs;
using BusinessLogic.Validation;
using BusinessObjects.Entities;
using BusinessObjects.Enums;
using DataAcessLayer.Repositories;

namespace BusinessLogic.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IFileSignatureValidator _fileSignatureValidator;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly ITextChunkingService _textChunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;

    public DocumentService(
        IDocumentRepository documentRepository,
        IUserRepository userRepository,
        IFileSignatureValidator fileSignatureValidator,
        IDocumentTextExtractor textExtractor,
        ITextChunkingService textChunkingService,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore)
    {
        _documentRepository = documentRepository;
        _userRepository = userRepository;
        _fileSignatureValidator = fileSignatureValidator;
        _textExtractor = textExtractor;
        _textChunkingService = textChunkingService;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
    }

    public async Task<IReadOnlyList<DocumentListItemDto>> GetDocumentsAsync(
        DocumentFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Document> documents = await _documentRepository.GetDocumentsAsync(
            filter.SubjectId,
            filter.ChapterId,
            filter.SearchTerm,
            filter.UploadedByUserId,
            cancellationToken);

        return documents.Select(MapListItem).ToList();
    }

    public async Task<DocumentDetailsDto?> GetDocumentDetailsAsync(
        int documentId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        Document? document = await _documentRepository.GetDocumentByIdAsync(
            documentId,
            includeChunks: true,
            cancellationToken);

        return document is null || document.UploadedByUserId != userId
            ? null
            : MapDetails(document);
    }

    public async Task<DocumentFileDto?> GetDocumentFileAsync(
        int documentId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        Document? document = await _documentRepository.GetDocumentByIdAsync(
            documentId,
            includeChunks: false,
            cancellationToken);

        return document is null || document.UploadedByUserId != userId
            ? null
            : new DocumentFileDto(
                document.DocumentId,
                document.OriginalFileName,
                document.StoragePath,
                document.ContentType);
    }

    public async Task<IReadOnlyList<SubjectOptionDto>> GetSubjectsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Subject> subjects = await _documentRepository.GetSubjectsAsync(userId, cancellationToken);

        return subjects
            .Select(subject => new SubjectOptionDto(subject.SubjectId, subject.Code, subject.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<ChapterOptionDto>> GetChaptersAsync(
        int? subjectId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Chapter> chapters = await _documentRepository.GetChaptersAsync(
            subjectId,
            userId,
            cancellationToken);

        return chapters
            .Select(chapter => new ChapterOptionDto(
                chapter.ChapterId,
                chapter.SubjectId,
                chapter.ChapterNumber,
                chapter.Title))
            .ToList();
    }

    public async Task<DocumentUploadResultDto> UploadDocumentAsync(
        DocumentUploadDto upload,
        CancellationToken cancellationToken = default)
    {
        await ValidateUploadAsync(upload, cancellationToken);

        string originalFileName = Path.GetFileName(upload.OriginalFileName);
        string fileExtension = Path.GetExtension(originalFileName).ToLowerInvariant();
        string storedFileName = $"{Guid.NewGuid():N}{fileExtension}";
        string tempRelativePath = Path.Combine("uploads", "_temp", storedFileName).Replace('\\', '/');
        string tempPhysicalPath = GetSafePhysicalPath(upload.WebRootPath, tempRelativePath);
        string? physicalPath = null;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tempPhysicalPath)!);

            await using (FileStream output = File.Create(tempPhysicalPath))
            {
                await upload.FileStream.CopyToAsync(output, cancellationToken);
            }

            if (!_fileSignatureValidator.HasValidSignature(tempPhysicalPath, fileExtension))
            {
                throw new BusinessValidationException(
                [
                    new ValidationError("File", "Nội dung file không khớp với định dạng đã chọn.")
                ]);
            }

            Chapter chapter = await _documentRepository.GetOrCreateChapterAsync(
                upload.SubjectId,
                upload.UploadedByUserId,
                upload.ChapterTitle,
                cancellationToken);
            await _documentRepository.SaveChangesAsync(cancellationToken);

            string relativePath = Path.Combine(
                "uploads",
                upload.UploadedByUserId.ToString(),
                upload.SubjectId.ToString(),
                chapter.ChapterId.ToString(),
                storedFileName).Replace('\\', '/');
            physicalPath = GetSafePhysicalPath(upload.WebRootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
            File.Move(tempPhysicalPath, physicalPath, overwrite: true);

            var document = new Document
            {
                UploadedByUserId = upload.UploadedByUserId,
                SubjectId = upload.SubjectId,
                ChapterId = chapter.ChapterId,
                Title = upload.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(upload.Description) ? null : upload.Description.Trim(),
                OriginalFileName = originalFileName,
                StoredFileName = storedFileName,
                StoragePath = relativePath,
                ContentType = string.IsNullOrWhiteSpace(upload.ContentType)
                    ? "application/octet-stream"
                    : upload.ContentType,
                FileExtension = fileExtension,
                FileSizeBytes = upload.FileSizeBytes,
                ProcessingStatus = DocumentProcessingStatus.Uploaded.ToString(),
                UploadedAt = DateTime.UtcNow
            };

            await ProcessDocumentTextAsync(document, physicalPath, cancellationToken);
            await _documentRepository.AddDocumentAsync(document, cancellationToken);
            await _documentRepository.SaveChangesAsync(cancellationToken);
            await StoreVectorsIfReadyAsync(document, upload.WebRootPath, cancellationToken);
            await _documentRepository.SaveChangesAsync(cancellationToken);

            return new DocumentUploadResultDto(
                document.DocumentId,
                document.ProcessingStatus,
                document.ChunkCount);
        }
        catch
        {
            DeleteFileIfExists(tempPhysicalPath);

            if (physicalPath is not null)
            {
                DeleteFileIfExists(physicalPath);
            }

            throw;
        }
    }

    public async Task<bool> DeleteDocumentAsync(
        int documentId,
        int userId,
        string webRootPath,
        CancellationToken cancellationToken = default)
    {
        Document? document = await _documentRepository.GetDocumentByIdAsync(
            documentId,
            includeChunks: false,
            cancellationToken);

        if (document is null || document.UploadedByUserId != userId)
        {
            return false;
        }

        string physicalPath = GetSafePhysicalPath(webRootPath, document.StoragePath);

        _documentRepository.DeleteDocument(document);
        await _documentRepository.SaveChangesAsync(cancellationToken);
        await DeleteVectorsIfExistsAsync(document.DocumentId, webRootPath, cancellationToken);

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return true;
    }

    public async Task<DocumentUploadResultDto?> ReIndexDocumentAsync(
        int documentId,
        int userId,
        string webRootPath,
        CancellationToken cancellationToken = default)
    {
        Document? document = await _documentRepository.GetDocumentByIdAsync(
            documentId,
            includeChunks: true,
            cancellationToken);

        if (document is null || document.UploadedByUserId != userId)
        {
            return null;
        }

        string physicalPath = GetSafePhysicalPath(webRootPath, document.StoragePath);

        if (!File.Exists(physicalPath))
        {
            throw new BusinessValidationException(
            [
                new ValidationError("File", "Không tìm thấy file vật lý để xử lý lại.")
            ]);
        }

        await DeleteVectorsIfExistsAsync(document.DocumentId, webRootPath, cancellationToken);
        document.DocumentChunks.Clear();
        document.ChunkCount = 0;
        document.ProcessingStatus = DocumentProcessingStatus.Uploaded.ToString();
        document.UpdatedAt = DateTime.UtcNow;
        await _documentRepository.SaveChangesAsync(cancellationToken);

        await ProcessDocumentTextAsync(document, physicalPath, cancellationToken);
        await _documentRepository.SaveChangesAsync(cancellationToken);
        await StoreVectorsIfReadyAsync(document, webRootPath, cancellationToken);
        await _documentRepository.SaveChangesAsync(cancellationToken);

        return new DocumentUploadResultDto(
            document.DocumentId,
            document.ProcessingStatus,
            document.ChunkCount);
    }

    private async Task ValidateUploadAsync(
        DocumentUploadDto upload,
        CancellationToken cancellationToken)
    {
        var errors = new List<ValidationError>();

        if (upload.SubjectId <= 0)
        {
            errors.Add(new ValidationError(nameof(upload.SubjectId), "Vui lòng chọn môn học."));
        }
        else if (!await _documentRepository.SubjectExistsAsync(upload.SubjectId, upload.UploadedByUserId, cancellationToken))
        {
            errors.Add(new ValidationError(nameof(upload.SubjectId), "Môn học không tồn tại hoặc đã bị vô hiệu hóa."));
        }

        if (upload.UploadedByUserId <= 0)
        {
            errors.Add(new ValidationError(nameof(upload.UploadedByUserId), "Không xác định được người upload."));
        }
        else
        {
            AppUser? uploadedByUser = await _userRepository.GetByIdAsync(
                upload.UploadedByUserId,
                cancellationToken);

            if (uploadedByUser is null || !uploadedByUser.IsActive)
            {
                errors.Add(new ValidationError(nameof(upload.UploadedByUserId), "Người upload không tồn tại hoặc đã bị vô hiệu hóa."));
            }
        }

        ValidateText(errors, nameof(upload.ChapterTitle), upload.ChapterTitle, 250, required: true);
        ValidateMinimumLength(errors, nameof(upload.ChapterTitle), upload.ChapterTitle, "Tên chương", 2);
        ValidateText(errors, nameof(upload.Title), upload.Title, DocumentUploadRules.MaxTitleLength, required: true);
        ValidateMinimumLength(errors, nameof(upload.Title), upload.Title, "Tiêu đề", 2);
        ValidateText(errors, nameof(upload.Description), upload.Description, DocumentUploadRules.MaxDescriptionLength, required: false);
        ValidateFileMetadata(errors, upload);

        if (errors.Count > 0)
        {
            throw new BusinessValidationException(errors);
        }
    }

    private static void ValidateText(
        ICollection<ValidationError> errors,
        string fieldName,
        string? value,
        int maxLength,
        bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors.Add(new ValidationError(fieldName, "Trường này là bắt buộc."));
            }

            return;
        }

        string trimmedValue = value.Trim();

        if (trimmedValue.Length > maxLength)
        {
            errors.Add(new ValidationError(fieldName, $"Không được vượt quá {maxLength} ký tự."));
        }

        bool hasInvalidControlCharacter = trimmedValue.Any(character =>
            char.IsControl(character) && character is not '\r' and not '\n' and not '\t');

        if (hasInvalidControlCharacter)
        {
            errors.Add(new ValidationError(fieldName, "Không được chứa ký tự điều khiển không hợp lệ."));
        }
    }

    private static void ValidateMinimumLength(
        ICollection<ValidationError> errors,
        string fieldName,
        string? value,
        string label,
        int minLength)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length < minLength)
        {
            errors.Add(new ValidationError(fieldName, $"{label} phải có ít nhất {minLength} ký tự."));
        }
    }

    private static void ValidateFileMetadata(
        ICollection<ValidationError> errors,
        DocumentUploadDto upload)
    {
        if (upload.FileStream is null || !upload.FileStream.CanRead)
        {
            errors.Add(new ValidationError("File", "File không đọc được."));
            return;
        }

        if (upload.FileSizeBytes <= 0)
        {
            errors.Add(new ValidationError("File", "File không được rỗng."));
        }
        else if (upload.FileSizeBytes > DocumentUploadRules.MaxFileSizeBytes)
        {
            errors.Add(new ValidationError("File", "File tối đa là 100 MB."));
        }

        if (string.IsNullOrWhiteSpace(upload.OriginalFileName))
        {
            errors.Add(new ValidationError("File", "Tên file không hợp lệ."));
            return;
        }

        string fileName = Path.GetFileName(upload.OriginalFileName);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            errors.Add(new ValidationError("File", "Tên file không hợp lệ."));
            return;
        }

        if (fileName.Length > DocumentUploadRules.MaxOriginalFileNameLength)
        {
            errors.Add(new ValidationError("File", $"Tên file không được vượt quá {DocumentUploadRules.MaxOriginalFileNameLength} ký tự."));
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            errors.Add(new ValidationError("File", "Tên file chứa ký tự không hợp lệ."));
        }

        string fileExtension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!DocumentUploadRules.IsAllowedExtension(fileExtension))
        {
            errors.Add(new ValidationError("File", "Chỉ hỗ trợ PDF, DOCX và PPTX."));
            return;
        }

        if (!DocumentUploadRules.IsAllowedContentType(fileExtension, upload.ContentType))
        {
            errors.Add(new ValidationError("File", "Content-Type không khớp với phần mở rộng file."));
        }
    }

    private async Task ProcessDocumentTextAsync(
        Document document,
        string physicalPath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_textExtractor.CanExtract(document.FileExtension))
            {
                string text = await _textExtractor.ExtractTextAsync(
                    physicalPath,
                    document.FileExtension,
                    cancellationToken);

                IReadOnlyList<TextChunk> chunks = _textChunkingService.Chunk(text);
                AddChunks(document, chunks);
            }

            document.ProcessingStatus = DocumentProcessingStatus.Uploaded.ToString();
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            document.ProcessingStatus = DocumentProcessingStatus.Failed.ToString();
            document.ChunkCount = 0;
            document.DocumentChunks.Clear();
        }
    }

    private async Task StoreVectorsIfReadyAsync(
        Document document,
        string storageRootPath,
        CancellationToken cancellationToken)
    {
        if (document.ProcessingStatus == DocumentProcessingStatus.Failed.ToString() ||
            document.DocumentChunks.Count == 0)
        {
            return;
        }

        try
        {
            foreach (DocumentChunk chunk in document.DocumentChunks.OrderBy(chunk => chunk.ChunkIndex))
            {
                chunk.VectorId ??= $"vec_{Guid.NewGuid():N}";
                float[] embedding = await _embeddingService.GenerateEmbeddingAsync(
                    chunk.Content,
                    cancellationToken);

                await _vectorStore.UpsertAsync(
                    new VectorRecord(
                        chunk.VectorId,
                        embedding,
                        document.DocumentId,
                        chunk.DocumentChunkId),
                    storageRootPath,
                    cancellationToken);
            }

            document.ProcessingStatus = DocumentProcessingStatus.Indexed.ToString();
            document.UpdatedAt = DateTime.UtcNow;
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            document.ProcessingStatus = DocumentProcessingStatus.Failed.ToString();
            document.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task DeleteVectorsIfExistsAsync(
        int documentId,
        string storageRootPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await _vectorStore.DeleteByDocumentIdAsync(documentId, storageRootPath, cancellationToken);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            // Metadata deletion must not be blocked by a stale local vector-store file.
        }
    }

    private static void AddChunks(Document document, IReadOnlyList<TextChunk> chunks)
    {
        foreach (TextChunk chunk in chunks)
        {
            document.DocumentChunks.Add(new DocumentChunk
            {
                ChunkIndex = chunk.Index,
                Content = chunk.Content,
                TokenCount = chunk.EstimatedTokenCount,
                VectorId = $"vec_{Guid.NewGuid():N}",
                CreatedAt = DateTime.UtcNow
            });
        }

        document.ChunkCount = chunks.Count;
    }

    private static string GetSafePhysicalPath(string webRootPath, string relativePath)
    {
        string root = Path.GetFullPath(webRootPath);
        string combined = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string fullPath = Path.GetFullPath(combined);

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        return fullPath;
    }

    private static void DeleteFileIfExists(string physicalPath)
    {
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }
    }

    private static DocumentListItemDto MapListItem(Document document)
    {
        return new DocumentListItemDto(
            document.DocumentId,
            document.Title,
            document.OriginalFileName,
            document.ContentType,
            document.FileExtension,
            document.FileSizeBytes,
            document.ProcessingStatus,
            document.ChunkCount,
            document.UploadedAt,
            document.Subject.Name,
            document.Chapter?.Title,
            document.UploadedByUser?.FullName);
    }

    private static DocumentDetailsDto MapDetails(Document document)
    {
        IReadOnlyList<DocumentChunkDto> chunks = document.DocumentChunks
            .OrderBy(chunk => chunk.ChunkIndex)
            .Select(chunk => new DocumentChunkDto(
                chunk.DocumentChunkId,
                chunk.ChunkIndex,
                chunk.Content,
                chunk.TokenCount,
                chunk.PageNumber,
                chunk.VectorId))
            .ToList();

        return new DocumentDetailsDto(
            document.DocumentId,
            document.Title,
            document.Description,
            document.OriginalFileName,
            document.StoredFileName,
            document.StoragePath,
            document.ContentType,
            document.FileExtension,
            document.FileSizeBytes,
            document.ProcessingStatus,
            document.ChunkCount,
            document.UploadedAt,
            document.Subject.Name,
            document.Chapter?.Title,
            document.UploadedByUser?.FullName,
            chunks);
    }
}
