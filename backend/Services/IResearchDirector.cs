using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using backend.Models;

namespace backend.Services;

public interface IResearchDirector
{
    Task<(ResearchPlan Plan, List<SourceInfo> Sources)> ExecuteResearchAsync(string connectionString, string userQuery, string conversationHistory, CancellationToken cancellationToken = default);
}
