using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using backend.Models;

namespace backend.Services;

public interface IWorkspaceService
{
    Task<(WorkspaceSummary Summary, List<SourceInfo> AnalyzedSources)> ProcessWorkspaceRequestAsync(string connectionString, string userQuery, CancellationToken cancellationToken = default);
}
