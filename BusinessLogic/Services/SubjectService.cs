using BusinessLogic.DTOs;
using BusinessLogic.Validation;
using BusinessObjects.Entities;
using DataAcessLayer.Repositories;

namespace BusinessLogic.Services;

public sealed class SubjectService : ISubjectService
{
    private const int MaxSubjectNameLength = 200;
    private const int MaxDescriptionLength = 1000;

    private readonly ISubjectRepository _subjectRepository;

    public SubjectService(ISubjectRepository subjectRepository)
    {
        _subjectRepository = subjectRepository;
    }

    public async Task<IReadOnlyList<SubjectListItemDto>> GetSubjectsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Subject> subjects = await _subjectRepository.GetSubjectsAsync(userId, cancellationToken);

        return subjects.Select(MapSubjectListItem).ToList();
    }

    public async Task<SubjectDetailsDto?> GetSubjectDetailsAsync(
        int subjectId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        Subject? subject = await _subjectRepository.GetSubjectByIdAsync(subjectId, userId, cancellationToken);

        if (subject is null)
        {
            return null;
        }

        return new SubjectDetailsDto(
            subject.SubjectId,
            subject.Name,
            subject.Description,
            subject.CreatedAt,
            subject.Chapters
                .OrderBy(chapter => chapter.SortOrder)
                .ThenBy(chapter => chapter.ChapterNumber)
                .Select(MapChapterListItem)
                .ToList());
    }

    public async Task<int> CreateSubjectAsync(
        SubjectUpsertDto dto,
        CancellationToken cancellationToken = default)
    {
        await ValidateSubjectAsync(dto, cancellationToken);

        var subject = new Subject
        {
            Code = GenerateSubjectCode(dto.Name),
            Name = dto.Name.Trim(),
            Description = NormalizeNullable(dto.Description),
            CreatedByUserId = dto.UserId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _subjectRepository.AddSubjectAsync(subject, cancellationToken);
        await _subjectRepository.SaveChangesAsync(cancellationToken);

        return subject.SubjectId;
    }

    public async Task<bool> UpdateSubjectAsync(
        SubjectUpsertDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!dto.SubjectId.HasValue)
        {
            throw new BusinessValidationException(
            [
                new ValidationError(nameof(dto.SubjectId), "Không xác định được môn học cần cập nhật.")
            ]);
        }

        Subject? subject = await _subjectRepository.GetSubjectByIdAsync(
            dto.SubjectId.Value,
            dto.UserId,
            cancellationToken);

        if (subject is null)
        {
            return false;
        }

        await ValidateSubjectAsync(dto, cancellationToken);

        subject.Name = dto.Name.Trim();
        subject.Description = NormalizeNullable(dto.Description);

        await _subjectRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteSubjectAsync(
        int subjectId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        Subject? subject = await _subjectRepository.GetSubjectByIdAsync(subjectId, userId, cancellationToken);

        if (subject is null)
        {
            return false;
        }

        if (subject.Chapters.Count > 0 || subject.Documents.Count > 0)
        {
            throw new BusinessValidationException(
            [
                new ValidationError(string.Empty, "Không thể xóa môn học đang có chương hoặc tài liệu. Hãy xóa tài liệu và chương trước.")
            ]);
        }

        _subjectRepository.DeleteSubject(subject);
        await _subjectRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteChapterAsync(
        int subjectId,
        int chapterId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        Chapter? chapter = await _subjectRepository.GetChapterByIdAsync(chapterId, userId, cancellationToken);

        if (chapter is null || chapter.SubjectId != subjectId)
        {
            return false;
        }

        if (chapter.Documents.Count > 0)
        {
            throw new BusinessValidationException(
            [
                new ValidationError(string.Empty, "Không thể xóa chương đang có tài liệu. Hãy xóa tài liệu trong chương này trước.")
            ]);
        }

        _subjectRepository.DeleteChapter(chapter);
        await _subjectRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task ValidateSubjectAsync(
        SubjectUpsertDto dto,
        CancellationToken cancellationToken)
    {
        var errors = new List<ValidationError>();

        ValidateText(errors, nameof(dto.Name), dto.Name, "Tên môn học", 2, MaxSubjectNameLength, required: true);
        ValidateText(errors, nameof(dto.Description), dto.Description, "Mô tả", 0, MaxDescriptionLength, required: false);

        if (!string.IsNullOrWhiteSpace(dto.Name) &&
            await _subjectRepository.SubjectNameExistsAsync(
                dto.Name,
                dto.UserId,
                dto.SubjectId,
                cancellationToken))
        {
            errors.Add(new ValidationError(nameof(dto.Name), "Tên môn học đã tồn tại."));
        }

        ThrowIfAny(errors);
    }

    private static void ValidateText(
        ICollection<ValidationError> errors,
        string fieldName,
        string? value,
        string label,
        int minLength,
        int maxLength,
        bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors.Add(new ValidationError(fieldName, $"{label} là bắt buộc."));
            }

            return;
        }

        string trimmedValue = value.Trim();

        if (trimmedValue.Length < minLength || trimmedValue.Length > maxLength)
        {
            errors.Add(new ValidationError(fieldName, $"{label} phải từ {minLength} đến {maxLength} ký tự."));
        }
    }

    private static void ThrowIfAny(IReadOnlyCollection<ValidationError> errors)
    {
        if (errors.Count > 0)
        {
            throw new BusinessValidationException(errors);
        }
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GenerateSubjectCode(string name)
    {
        string compactName = new(name
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Take(20)
            .ToArray());

        return string.IsNullOrWhiteSpace(compactName)
            ? $"SUB-{Guid.NewGuid():N}"[..24]
            : $"{compactName.ToUpperInvariant()}-{Guid.NewGuid():N}"[..24];
    }

    private static SubjectListItemDto MapSubjectListItem(Subject subject)
    {
        return new SubjectListItemDto(
            subject.SubjectId,
            subject.Name,
            subject.Description,
            subject.Chapters.Count,
            subject.Documents.Count,
            subject.CreatedAt);
    }

    private static ChapterListItemDto MapChapterListItem(Chapter chapter)
    {
        return new ChapterListItemDto(
            chapter.ChapterId,
            chapter.SubjectId,
            chapter.ChapterNumber,
            chapter.Title,
            chapter.Description,
            chapter.Documents.Count,
            chapter.CreatedAt);
    }
}
