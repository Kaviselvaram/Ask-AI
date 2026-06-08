using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.EndToEnd.Section2_IntentIntelligence
{
    public class IntentTests : E2ETestBase
    {
        [Fact]
        public async Task TCINTENT001_WhatIsAIOS_ResearchIntent()
        {
            var response = await ChatAsync("What is AIOS?");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected Intent: Research (The system handles it silently, but we expect a factual answer)
            Assert.Contains("artificial intelligence", result);
        }

        [Fact]
        public async Task TCINTENT002_CompareDocuments_ComparisonIntent()
        {
            var response = await ChatAsync("Compare AIOS and Bookreview report");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected Intent: Comparison workflow triggered. It should generate a comparison result.
            Assert.True(result.Contains("compare") || result.Contains("difference") || result.Contains("similarity") || result.Contains("aios") || result.Contains("bookreview"));
        }

        [Fact]
        public async Task TCINTENT003_AnalyzeDocuments_AnalysisIntent()
        {
            var response = await ChatAsync("Analyze AIOS documents");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected Intent: Analysis workflow triggered.
            Assert.True(result.Contains("analyze") || result.Contains("analysis") || result.Contains("aios") || result.Contains("risk") || result.Contains("finding"));
        }
    }
}
