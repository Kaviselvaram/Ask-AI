using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.EndToEnd.Section8_WorkspaceIntelligence
{
    public class WorkspaceTests : E2ETestBase
    {
        [Fact]
        public async Task TCWS001_WorkspaceSummary()
        {
            var response = await ChatAsync("Summarize my document vault.");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Workspace summary generated.
            Assert.True(result.Contains("document") || result.Contains("summary"));
        }

        [Fact]
        public async Task TCWS002_TCWS003_WorkspaceSearchAndRelationships()
        {
            var response = await ChatAsync("Show everything related to AIOS and which documents are related to it?");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Related entities and documents. Relationship discovery.
            Assert.True(result.Contains("related") || result.Contains("aios"));
        }
    }
}
