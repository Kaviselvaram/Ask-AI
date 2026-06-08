using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.EndToEnd.Section5_VerificationIntelligence
{
    public class VerificationTests : E2ETestBase
    {
        [Fact]
        public async Task TCVER001_HallucinationPrevention()
        {
            var response = await ChatAsync("Who founded AIOS?");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Insufficient evidence found.
            Assert.True(result.Contains("insufficient") || result.Contains("does not mention") || result.Contains("cannot answer"));
        }

        [Fact]
        public async Task TCVER002_EvidenceBasedResponse()
        {
            var response = await ChatAsync("What is AIOS?");
            var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: Verified answer
            Assert.True(result.Contains("artificial intelligence") || result.Contains("system"));
        }
    }
}
