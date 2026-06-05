using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using backend.Models;

namespace backend.Services;

public interface IVaultAnalysisService
{
    Task<(string VaultContext, List<SourceInfo> AnalyzedSources)> BuildVaultContextAsync(string connectionString, int chunksPerDocument = 3);
}
