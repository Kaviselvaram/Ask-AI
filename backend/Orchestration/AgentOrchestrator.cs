using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using backend.Agents;
using backend.Models;
using backend.Services;
using Microsoft.SemanticKernel;

namespace backend.Orchestration
{
    public class AgentOrchestrator
    {
        private readonly TaskClassifier _classifier;
        private readonly DynamicPlanner _planner;
        private readonly ReportGenerator _reportGenerator;
        private readonly Dictionary<string, BaseAgent> _agents;

        public AgentOrchestrator(
            TaskClassifier classifier, 
            DynamicPlanner planner, 
            ReportGenerator reportGenerator, 
            IEnumerable<BaseAgent> agents)
        {
            _classifier = classifier;
            _planner = planner;
            _reportGenerator = reportGenerator;
            _agents = agents.ToDictionary(a => a.AgentName, a => a);
        }

        public async Task<AgentState> ExecuteAsync(string query, int? documentId = null)
        {
            Console.WriteLine("=====================================");
            Console.WriteLine($"[Orchestrator] Starting workflow for: {query}");
            
            var state = new AgentState 
            { 
                OriginalQuery = query,
                RewrittenQuery = query // Assume already rewritten upstream, or rewrite here
            };

            // 1. Classification
            var classification = await _classifier.ClassifyTaskAsync(query);
            state.Intent = classification.TaskType;
            Console.WriteLine($"[Orchestrator] Intent classified as: {state.Intent} (Confidence: {classification.Confidence}%)");

            // 2. Planning
            state.ExecutionPlan = await _planner.GeneratePlanAsync(query, state.Intent);
            Console.WriteLine($"[Orchestrator] Execution Plan: {string.Join(" -> ", state.ExecutionPlan)}");

            // 3. Execution Loop
            foreach (var step in state.ExecutionPlan)
            {
                if (step == "report_generation")
                    continue; // Handled at the end

                if (_agents.TryGetValue(step, out var agent))
                {
                    try
                    {
                        await agent.ExecuteAsync(state);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Orchestrator] Agent {step} failed: {ex.Message}");
                        state.ErrorMessage = $"Agent {step} failed.";
                    }
                }
                else
                {
                    Console.WriteLine($"[Orchestrator] Unknown agent in plan: {step}");
                }
            }

            // 4. Verification and Confidence bounds checking
            if (_agents.TryGetValue("verification", out var verificationAgent))
            {
                if (!state.ExecutionPlan.Contains("verification"))
                {
                     await verificationAgent.ExecuteAsync(state);
                }
            }

            Console.WriteLine($"[Orchestrator] Global Confidence after Verification: {state.GlobalConfidenceScore:F1}%");

            // 5. Final Report Generation
            await _reportGenerator.GenerateFinalReportAsync(state);
            Console.WriteLine("[Orchestrator] Workflow complete.");
            Console.WriteLine("=====================================");

            return state;
        }
    }
}
