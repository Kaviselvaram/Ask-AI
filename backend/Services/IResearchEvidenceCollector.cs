using System.Collections.Generic;
using System.Threading.Tasks;
using backend.Models;

namespace backend.Services;

public interface IResearchEvidenceCollector
{
    Task<(string EvidenceContext, List<SourceInfo> CollectedSources, bool IsSufficient)> CollectEvidenceAsync(string connectionString, string userQuery);
}
