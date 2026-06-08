using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.EndToEnd.Section3_PlanningIntelligence
{
    public class PlanningTests : E2ETestBase
    {
        [Fact]
        public async Task TCPLAN001_AnalyzeAIOSAndIdentifyRisks()
        {
            var response = await ChatAsync("Analyze AIOS and identify risks.");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Planner decomposes task, Structured response generated
            Assert.True(result.Contains("risk") || result.Contains("objective") || result.Contains("finding") || result.Contains("recommendation") || result.Contains("aios"));
        }

        [Fact]
        public async Task TCPLAN002_CompareDocuments()
        {
            var response = await ChatAsync("Compare AIOS and Bookreview report.docx.");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Comparison plan executed
            Assert.True(result.Contains("compare") || result.Contains("difference") || result.Contains("similarity") || result.Contains("aios") || result.Contains("bookreview"));
        }
    }
}
