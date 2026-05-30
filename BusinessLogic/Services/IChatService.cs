using BusinessLogic.DTOs;

namespace BusinessLogic.Services;

public interface IChatService
{
    Task<ChatPageDto> GetChatPageAsync(
        int userId,
        ChatScopeDto scope,
        CancellationToken cancellationToken = default);

    Task<ChatAnswerDto> AskAsync(
        ChatAskDto dto,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteConversationAsync(
        int conversationId,
        int userId,
        CancellationToken cancellationToken = default);
}
