using System.Threading;
using System.Threading.Tasks;
using backend.Models;

namespace backend.Orchestration;

public interface IInsightEngine
{
    Task<InsightResult> AnalyzeAsync(string context, string userQuery, CancellationToken cancellationToken = default);
}
