namespace BusinessLogic.Services;

public sealed class FileStorageService : IFileStorageService
{
    public async Task<TempStoredFileDto> SaveUploadTempAsync(
        string webRootPath,
        string originalFileName,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        string safeOriginalFileName = Path.GetFileName(originalFileName);
        string fileExtension = Path.GetExtension(safeOriginalFileName).ToLowerInvariant();
        string storedFileName = $"{Guid.NewGuid():N}{fileExtension}";
        string tempRelativePath = Path.Combine("uploads", "_temp", storedFileName).Replace('\\', '/');
        string tempPhysicalPath = GetSafePhysicalPath(webRootPath, tempRelativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(tempPhysicalPath)!);

        await using (FileStream output = File.Create(tempPhysicalPath))
        {
            await fileStream.CopyToAsync(output, cancellationToken);
        }

        return new TempStoredFileDto(
            safeOriginalFileName,
            storedFileName,
            fileExtension,
            tempRelativePath,
            tempPhysicalPath);
    }

    public StoredFileDto MoveTempUploadToDocumentFolder(
        TempStoredFileDto tempFile,
        string webRootPath,
        int uploadedByUserId,
        int subjectId,
        int chapterId)
    {
        // Đường dẫn lưu theo user/subject/chapter để tách file vật lý theo owner và scope.
        string relativePath = Path.Combine(
            "uploads",
            uploadedByUserId.ToString(),
            subjectId.ToString(),
            chapterId.ToString(),
            tempFile.StoredFileName).Replace('\\', '/');
        string physicalPath = GetSafePhysicalPath(webRootPath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        File.Move(tempFile.TempPhysicalPath, physicalPath, overwrite: true);

        return new StoredFileDto(relativePath, physicalPath);
    }

    public string GetSafePhysicalPath(string webRootPath, string relativePath)
    {
        string root = Path.GetFullPath(webRootPath);
        string combined = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string fullPath = Path.GetFullPath(combined);

        // Chặn path traversal: path cuối cùng bắt buộc nằm trong web root.
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        return fullPath;
    }

    public bool FileExists(string physicalPath)
    {
        return File.Exists(physicalPath);
    }

    public void DeleteFileIfExists(string physicalPath)
    {
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }
    }

    public void DeleteDocumentFileIfExists(string webRootPath, string relativePath)
    {
        DeleteFileIfExists(GetSafePhysicalPath(webRootPath, relativePath));
    }
}
