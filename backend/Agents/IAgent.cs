using System.Threading;
using System.Threading.Tasks;
using backend.Models;

namespace backend.Agents;

public interface IAgent
{
    string Name { get; }
    Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default);
}
