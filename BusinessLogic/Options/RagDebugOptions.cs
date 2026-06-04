namespace BusinessLogic.Options;

public sealed class RagDebugOptions
{
    // Mặc định false để tránh log prompt/context/answer chứa nội dung nhạy cảm.
    public bool Enabled { get; init; }
}
