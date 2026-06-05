using System.Threading;
using System.Threading.Tasks;
using backend.Models;

namespace backend.Orchestration;

public interface IAgentOrchestrator
{
    Task<string> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default);
}
