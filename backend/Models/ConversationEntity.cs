using System;

namespace backend.Models
{
    public record ConversationEntity(
        string ConversationId,
        string EntityName,
        DateTime LastReferenced
    );
}
