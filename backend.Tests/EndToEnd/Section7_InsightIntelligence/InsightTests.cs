using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.EndToEnd.Section7_InsightIntelligence
{
    public class InsightTests : E2ETestBase
    {
        [Fact]
        public async Task TCINSIGHT001_GapDetection()
        {
            var response = await ChatAsync("Identify missing information in AIOS documents.");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Gaps identified.
            Assert.True(result.Contains("gap") || result.Contains("missing") || result.Contains("information"));
        }

        [Fact]
        public async Task TCINSIGHT002_ContradictionDetection()
        {
            var response = await ChatAsync("Analyze AIOS documents and identify contradictions.");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Contradictions or no contradictions.
            Assert.True(result.Contains("contradiction") || result.Contains("conflict"));
        }

        [Fact]
        public async Task TCINSIGHT003_DuplicateDetection()
        {
            var response = await ChatAsync("Find duplicate or highly similar documents.");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Duplicate findings generated.
            Assert.True(result.Contains("duplicate") || result.Contains("similar"));
        }

        [Fact]
        public async Task TCINSIGHT004_VaultAnalysis()
        {
            var response = await ChatAsync("Analyze all uploaded documents and identify common themes.");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Themes generated.
            Assert.True(result.Contains("theme") || result.Contains("common"));
        }
    }
}
