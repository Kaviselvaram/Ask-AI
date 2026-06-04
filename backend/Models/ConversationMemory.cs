using System;

namespace backend.Models
{
    public record ConversationMemory(
        string ConversationId,
        string Role,
        string Content,
        DateTime Timestamp
    );
}
