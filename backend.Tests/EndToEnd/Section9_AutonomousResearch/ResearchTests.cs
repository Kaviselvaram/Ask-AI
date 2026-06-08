using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.EndToEnd.Section9_AutonomousResearch
{
    public class ResearchTests : E2ETestBase
    {
        [Fact]
        public async Task TCAR001_AnalyzeProductRisks()
        {
            var response = await ChatAsync("Analyze all AIOS documents and identify product risks.");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Objective, Findings, Recommendations
            Assert.True(result.Contains("objective") || result.Contains("finding") || result.Contains("recommendation") || result.Contains("risk"));
        }

        [Fact]
        public async Task TCAR002_EvaluateOpportunities()
        {
            var response = await ChatAsync("Evaluate AIOS and identify opportunities.");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Opportunities and recommendations.
            Assert.True(result.Contains("opportunity") || result.Contains("recommendation"));
        }

        [Fact]
        public async Task TCAR003_ConsultantEvaluation()
        {
            string prompt = "Act as a consultant and evaluate AIOS. Identify: 1. Risks 2. Opportunities 3. Missing Components 4. Strategic Recommendations";
            var response = await ChatAsync(prompt);
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Executive assessment generated.
            Assert.True(result.Contains("risk") || result.Contains("opportunity") || result.Contains("recommendation"));
        }
    }
}
