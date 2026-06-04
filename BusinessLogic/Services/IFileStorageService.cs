namespace BusinessLogic.Services;

public interface IFileStorageService
{
    Task<TempStoredFileDto> SaveUploadTempAsync(
        string webRootPath,
        string originalFileName,
        Stream fileStream,
        CancellationToken cancellationToken = default);

    StoredFileDto MoveTempUploadToDocumentFolder(
        TempStoredFileDto tempFile,
        string webRootPath,
        int uploadedByUserId,
        int subjectId,
        int chapterId);

    string GetSafePhysicalPath(string webRootPath, string relativePath);

    bool FileExists(string physicalPath);

    void DeleteFileIfExists(string physicalPath);

    void DeleteDocumentFileIfExists(string webRootPath, string relativePath);
}

public sealed record TempStoredFileDto(
    string OriginalFileName,
    string StoredFileName,
    string FileExtension,
    string TempRelativePath,
    string TempPhysicalPath);

public sealed record StoredFileDto(
    string RelativePath,
    string PhysicalPath);
