using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using backend.Models;
using backend.Orchestration;
using backend.Agents;

namespace backend.Services;

public class ResearchDirector : IResearchDirector
{
    private readonly IAgentOrchestrator _agentOrchestrator;
    private readonly IInsightEngine _insightEngine;
    private readonly IVaultAnalysisService _vaultAnalysisService;
    private readonly Kernel _kernel;

    public ResearchDirector(
        IAgentOrchestrator agentOrchestrator,
        IInsightEngine insightEngine,
        IVaultAnalysisService vaultAnalysisService,
        Kernel kernel)
    {
        _agentOrchestrator = agentOrchestrator;
        _insightEngine = insightEngine;
        _vaultAnalysisService = vaultAnalysisService;
        _kernel = kernel;
    }

    public async Task<(ResearchPlan Plan, List<SourceInfo> Sources)> ExecuteResearchAsync(string connectionString, string userQuery, string conversationHistory, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("RESEARCH DIRECTOR STARTED");
        Console.WriteLine("RESEARCH PLAN CREATED");

        // 1. Gather Vault Context
        var vaultAnalysisResult = await _vaultAnalysisService.BuildVaultContextAsync(connectionString, 3);
        string vaultContext = vaultAnalysisResult.VaultContext;

        // 2. Parallel Evidence Collection
        var agentContext = new AgentContext(
            Query: userQuery,
            Intent: "research comparison", // Forces both Research and Comparison agents
            ExecutionPlan: new ExecutionPlan("Aggregate Research", "Comprehensive", new List<string>(), 5, true, true, false),
            RetrievedContext: vaultContext,
            ConversationContext: conversationHistory
        );

        Task<string> agentsTask = _agentOrchestrator.ExecuteAsync(agentContext, cancellationToken);
        Task<InsightResult> insightTask = _insightEngine.AnalyzeAsync(vaultContext, userQuery, cancellationToken);

        await Task.WhenAll(agentsTask, insightTask);

        string agentsOutput = agentsTask.Result ?? "No agent findings.";
        var insightResult = insightTask.Result;

        Console.WriteLine("EVIDENCE COLLECTED");

        string aggregatedEvidence = $"--- AGENT FINDINGS ---\n{agentsOutput}\n\n";
        aggregatedEvidence += "--- VAULT INSIGHTS ---\n";
        if (insightResult?.Themes?.Count > 0) aggregatedEvidence += $"Themes: {string.Join(", ", insightResult.Themes)}\n";
        if (insightResult?.Contradictions?.Count > 0) aggregatedEvidence += $"Contradictions: {string.Join(", ", insightResult.Contradictions)}\n";
        if (insightResult?.Gaps?.Count > 0) aggregatedEvidence += $"Gaps: {string.Join(", ", insightResult.Gaps)}\n";
        
        // 3. Synthesis Generation
        string promptTemplate = await File.ReadAllTextAsync("Prompts/ResearchDirectorPrompt.txt", cancellationToken);
        var promptSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(ResearchPlan),
            Temperature = 0.2, 
            MaxTokens = 2500
        };

        var arguments = new KernelArguments(promptSettings)
        {
            ["evidence"] = aggregatedEvidence,
            ["query"] = userQuery
        };

        var result = await _kernel.InvokePromptAsync(promptTemplate, arguments, templateFormat: "semantic-kernel", cancellationToken: cancellationToken);
        string resultJson = result.GetValue<string>() ?? "{}";

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        ResearchPlan plan;
        try
        {
            plan = JsonSerializer.Deserialize<ResearchPlan>(resultJson, options) ?? new ResearchPlan();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RESEARCH DIRECTOR FAILED TO PARSE JSON: {ex.Message}");
            plan = new ResearchPlan { Objective = "Analyze Data", Findings = new List<string> { "Failed to parse research data." } };
        }

        Console.WriteLine("FINDINGS GENERATED");
        Console.WriteLine("RECOMMENDATIONS GENERATED");
        Console.WriteLine("RESEARCH DIRECTOR COMPLETED");

        return (plan, vaultAnalysisResult.AnalyzedSources);
    }
}
