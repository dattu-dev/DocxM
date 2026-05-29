namespace BusinessLogic.Validation;

public sealed record ValidationError(
    string FieldName,
    string ErrorMessage);
