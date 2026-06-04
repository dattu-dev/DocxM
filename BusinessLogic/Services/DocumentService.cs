using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
    private readonly IFileStorageService _fileStorageService;
    private readonly IDocumentProcessingService _documentProcessingService;

    public DocumentService(
        IDocumentRepository documentRepository,
        IUserRepository userRepository,
        IFileSignatureValidator fileSignatureValidator,
        IFileStorageService fileStorageService,
        IDocumentProcessingService documentProcessingService)
    {
        _documentRepository = documentRepository;
        _userRepository = userRepository;
        _fileSignatureValidator = fileSignatureValidator;
        _fileStorageService = fileStorageService;
        _documentProcessingService = documentProcessingService;
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
            filter.ViewerUserId,
            cancellationToken);

        return documents.Select(MapListItem).ToList();
    }

    public async Task<DocumentDetailsDto?> GetDocumentDetailsAsync(
        int documentId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        Document? document = await _documentRepository.GetDocumentByIdAsync(
            documentId,
            includeChunks: true,
            cancellationToken);

        if (document is null || !CanAccessDocument(document, ownerUserId, viewerUserId))
        {
            return null;
        }

        return MapDetails(document);
    }

    public async Task<DocumentFileDto?> GetDocumentFileAsync(
        int documentId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        // Preview và download đều đi qua đây để dùng chung logic kiểm quyền.
        Document? document = await _documentRepository.GetDocumentByIdAsync(
            documentId,
            includeChunks: false,
            cancellationToken);

        if (document is null || !CanAccessDocument(document, ownerUserId, viewerUserId))
        {
            return null;
        }

        return new DocumentFileDto(
            document.DocumentId,
            document.OriginalFileName,
            document.StoragePath,
            document.ContentType);
    }

    public async Task<IReadOnlyList<SubjectOptionDto>> GetSubjectsAsync(
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Subject> subjects = await _documentRepository.GetSubjectsAsync(ownerUserId, viewerUserId, cancellationToken);

        return subjects
            .Select(subject => new SubjectOptionDto(subject.SubjectId, subject.Code, subject.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<ChapterOptionDto>> GetChaptersAsync(
        int? subjectId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Chapter> chapters = await _documentRepository.GetChaptersAsync(
            subjectId,
            ownerUserId,
            viewerUserId,
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

        // Ghi file tạm trước, chỉ move vào thư mục chính sau khi signature hợp lệ.
        TempStoredFileDto? tempFile = null;
        StoredFileDto? storedFile = null;

        try
        {
            tempFile = await _fileStorageService.SaveUploadTempAsync(
                upload.WebRootPath,
                upload.OriginalFileName,
                upload.FileStream,
                cancellationToken);

            if (!_fileSignatureValidator.HasValidSignature(tempFile.TempPhysicalPath, tempFile.FileExtension))
            {
                // Không tin extension/content-type nếu nội dung file không mở được đúng định dạng.
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

            storedFile = _fileStorageService.MoveTempUploadToDocumentFolder(
                tempFile,
                upload.WebRootPath,
                upload.UploadedByUserId,
                upload.SubjectId,
                chapter.ChapterId);

            var document = new Document
            {
                UploadedByUserId = upload.UploadedByUserId,
                SubjectId = upload.SubjectId,
                ChapterId = chapter.ChapterId,
                Title = upload.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(upload.Description) ? null : upload.Description.Trim(),
                OriginalFileName = tempFile.OriginalFileName,
                StoredFileName = tempFile.StoredFileName,
                StoragePath = storedFile.RelativePath,
                ContentType = string.IsNullOrWhiteSpace(upload.ContentType)
                    ? "application/octet-stream"
                    : upload.ContentType,
                FileExtension = tempFile.FileExtension,
                FileSizeBytes = upload.FileSizeBytes,
                ProcessingStatus = DocumentProcessingStatus.Uploaded.ToString(),
                UploadedAt = DateTime.UtcNow
            };

            DocumentTextProcessingResult textProcessingResult = await _documentProcessingService.ExtractAndChunkAsync(
                storedFile.PhysicalPath,
                document.FileExtension,
                document.OriginalFileName,
                document.Title,
                cancellationToken);
            ApplyTextProcessingResult(document, textProcessingResult);
            await _documentRepository.AddDocumentAsync(document, cancellationToken);
            await _documentRepository.SaveChangesAsync(cancellationToken);
            DocumentVectorProcessingResult vectorProcessingResult = await _documentProcessingService.StoreVectorsIfReadyAsync(
                document,
                upload.WebRootPath,
                cancellationToken);
            ApplyVectorProcessingResult(document, vectorProcessingResult);
            await _documentRepository.SaveChangesAsync(cancellationToken);

            return new DocumentUploadResultDto(
                document.DocumentId,
                document.ProcessingStatus,
                document.ChunkCount);
        }
        catch
        {
            if (tempFile is not null)
            {
                _fileStorageService.DeleteFileIfExists(tempFile.TempPhysicalPath);
            }

            if (storedFile is not null)
            {
                _fileStorageService.DeleteFileIfExists(storedFile.PhysicalPath);
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

        string physicalPath = _fileStorageService.GetSafePhysicalPath(webRootPath, document.StoragePath);

        _documentRepository.DeleteDocument(document);
        await _documentRepository.SaveChangesAsync(cancellationToken);
        await _documentProcessingService.DeleteVectorsIfExistsAsync(document.DocumentId, webRootPath, cancellationToken);
        _fileStorageService.DeleteFileIfExists(physicalPath);

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

        // Reindex phải dùng lại file gốc đã lưu, không nhận lại file từ client.
        string physicalPath = _fileStorageService.GetSafePhysicalPath(webRootPath, document.StoragePath);

        if (!_fileStorageService.FileExists(physicalPath))
        {
            throw new BusinessValidationException(
            [
                new ValidationError("File", "Không tìm thấy file vật lý để xử lý lại.")
            ]);
        }

        await _documentProcessingService.DeleteVectorsIfExistsAsync(document.DocumentId, webRootPath, cancellationToken);
        document.DocumentChunks.Clear();
        document.ChunkCount = 0;
        document.ProcessingStatus = DocumentProcessingStatus.Uploaded.ToString();
        document.UpdatedAt = DateTime.UtcNow;
        await _documentRepository.SaveChangesAsync(cancellationToken);

        DocumentTextProcessingResult textProcessingResult = await _documentProcessingService.ExtractAndChunkAsync(
            physicalPath,
            document.FileExtension,
            document.OriginalFileName,
            document.Title,
            cancellationToken);
        ApplyTextProcessingResult(document, textProcessingResult);
        await _documentRepository.SaveChangesAsync(cancellationToken);
        DocumentVectorProcessingResult vectorProcessingResult = await _documentProcessingService.StoreVectorsIfReadyAsync(
            document,
            webRootPath,
            cancellationToken);
        ApplyVectorProcessingResult(document, vectorProcessingResult);
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

    private static void ApplyTextProcessingResult(
        Document document,
        DocumentTextProcessingResult result)
    {
        document.Title = result.Title;
        document.ProcessingStatus = result.ProcessingStatus;
        document.DocumentChunks.Clear();

        foreach (DocumentChunkProcessingResult chunk in result.Chunks)
        {
            document.DocumentChunks.Add(new DocumentChunk
            {
                ChunkIndex = chunk.ChunkIndex,
                Content = chunk.Content,
                TokenCount = chunk.TokenCount,
                SectionTitle = chunk.SectionTitle,
                VectorId = chunk.VectorId,
                CreatedAt = chunk.CreatedAt
            });
        }

        document.ChunkCount = result.Chunks.Count;
    }

    private static void ApplyVectorProcessingResult(
        Document document,
        DocumentVectorProcessingResult result)
    {
        document.ProcessingStatus = result.ProcessingStatus;

        if (result.UpdatedAt.HasValue)
        {
            document.UpdatedAt = result.UpdatedAt.Value;
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
                chunk.SectionTitle,
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

    private static bool CanAccessDocument(Document document, int? ownerUserId, int? viewerUserId)
    {
        if (ownerUserId.HasValue)
        {
            // Instructor chỉ xem document do chính mình upload.
            return document.UploadedByUserId == ownerUserId.Value;
        }

        if (viewerUserId.HasValue)
        {
            // Student chỉ xem document thuộc Subject đã được cấp quyền.
            return document.Subject.SubjectPermissions.Any(
                permission => permission.StudentUserId == viewerUserId.Value);
        }

        return false;
    }
}
