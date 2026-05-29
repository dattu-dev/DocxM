using System.ComponentModel.DataAnnotations;
using BusinessLogic.Services;

namespace Presentation.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class DocumentFileAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not IFormFile file)
        {
            return new ValidationResult("File không hợp lệ.");
        }

        if (file.Length <= 0)
        {
            return new ValidationResult("File không được rỗng.");
        }

        if (file.Length > DocumentUploadRules.MaxFileSizeBytes)
        {
            return new ValidationResult("File tối đa là 100 MB.");
        }

        string fileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new ValidationResult("Tên file không hợp lệ.");
        }

        if (fileName.Length > DocumentUploadRules.MaxOriginalFileNameLength)
        {
            return new ValidationResult($"Tên file không được vượt quá {DocumentUploadRules.MaxOriginalFileNameLength} ký tự.");
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return new ValidationResult("Tên file chứa ký tự không hợp lệ.");
        }

        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!DocumentUploadRules.IsAllowedExtension(extension))
        {
            return new ValidationResult("Chỉ hỗ trợ PDF, DOCX và PPTX.");
        }

        if (!DocumentUploadRules.IsAllowedContentType(extension, file.ContentType))
        {
            return new ValidationResult("Content-Type không khớp với phần mở rộng file.");
        }

        return ValidationResult.Success;
    }
}
