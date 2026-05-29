namespace AIService.Services;

public interface IFileSignatureValidator
{
    bool HasValidSignature(string filePath, string fileExtension);
}
