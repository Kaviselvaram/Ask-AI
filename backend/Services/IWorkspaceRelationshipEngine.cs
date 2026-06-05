using System.Threading;
using System.Threading.Tasks;
using backend.Models;

namespace backend.Services;

public interface IWorkspaceRelationshipEngine
{
    Task<WorkspaceSummary> BuildWorkspaceIntelligenceAsync(string context, string userQuery, CancellationToken cancellationToken = default);
}
