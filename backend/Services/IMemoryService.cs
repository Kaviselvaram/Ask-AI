using System.Collections.Generic;
using System.Threading.Tasks;
using backend.Models;

namespace backend.Services
{
    public interface IMemoryService
    {
        Task SaveMessageAsync(string conversationId, string role, string content);
        Task<List<ConversationMemory>> GetRecentMessagesAsync(string conversationId);
        Task ExtractAndSaveEntitiesAsync(string conversationId, string query);
        Task<List<ConversationEntity>> GetRecentEntitiesAsync(string conversationId);
    }
}
