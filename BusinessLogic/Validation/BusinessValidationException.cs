namespace BusinessLogic.Validation;

public sealed class BusinessValidationException : Exception
{
    public BusinessValidationException(IEnumerable<ValidationError> errors)
        : base("Đã xảy ra một hoặc nhiều lỗi validate.")
    {
        Errors = errors.ToList();
    }

    public IReadOnlyList<ValidationError> Errors { get; }
}
