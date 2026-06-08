using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.EndToEnd.Section6_MultiAgentIntelligence
{
    public class MultiAgentTests : E2ETestBase
    {
        [Fact]
        public async Task TCMA001_CompareDocuments()
        {
            var response = await ChatAsync("Compare AIOS_Revised_Product_Blueprint.txt and Bookreview report.txt");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Both documents analyzed, Comparison generated
            Assert.True(result.Contains("compare") || result.Contains("difference") || result.Contains("similarity"));
        }

        [Fact]
        public async Task TCMA002_CompareEntities()
        {
            var response = await ChatAsync("Compare AIOS and Bookreview report.txt");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Comparison Agent executed
            Assert.True(result.Contains("compare") || result.Contains("difference") || result.Contains("similarity") || result.Contains("aios"));
        }
    }
}
