using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using backend.Models;

namespace backend.Services;

public class WorkspaceService : IWorkspaceService
{
    private readonly IVaultAnalysisService _vaultAnalysisService;
    private readonly IWorkspaceRelationshipEngine _relationshipEngine;
    private readonly IWorkspaceCatalogBuilder _catalogBuilder;

    public WorkspaceService(IVaultAnalysisService vaultAnalysisService, IWorkspaceRelationshipEngine relationshipEngine, IWorkspaceCatalogBuilder catalogBuilder)
    {
        _vaultAnalysisService = vaultAnalysisService;
        _relationshipEngine = relationshipEngine;
        _catalogBuilder = catalogBuilder;
    }

    public async Task<(WorkspaceSummary Summary, List<SourceInfo> AnalyzedSources)> ProcessWorkspaceRequestAsync(string connectionString, string userQuery, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("WORKSPACE REQUEST DETECTED");

        // 1. Get raw context from Vault Analysis
        var vaultAnalysisResult = await _vaultAnalysisService.BuildVaultContextAsync(connectionString, 3);
        string vaultContext = vaultAnalysisResult.VaultContext;
        
        Console.WriteLine("DOCUMENTS LOADED");

        // 2. Build explicit catalog 
        var catalog = await _catalogBuilder.GetAvailableDocumentsAsync(connectionString);

        // 3. Build relationships and intelligent summary
        var summary = await _relationshipEngine.BuildWorkspaceIntelligenceAsync(vaultContext, userQuery, cancellationToken);
        
        // 4. Merge catalog info
        summary.TotalDocuments = catalog.Count;
        if (summary.Profiles == null) summary.Profiles = new List<DocumentProfile>();
        if (summary.Relationships == null) summary.Relationships = new List<DocumentRelationship>();

        Console.WriteLine("WORKSPACE RESPONSE GENERATED");
        
        return (summary, vaultAnalysisResult.AnalyzedSources);
    }
}
