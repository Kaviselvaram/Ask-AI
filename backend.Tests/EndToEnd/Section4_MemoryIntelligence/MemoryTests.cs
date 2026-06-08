using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.EndToEnd.Section4_MemoryIntelligence
{
    public class MemoryTests : E2ETestBase
    {
        [Fact]
        public async Task TCMEM001_AIOS_ContextRetained()
        {
            string sessionId = Guid.NewGuid().ToString();
            
            // Step 1
            await ChatAsync("What is AIOS?", sessionId);
            
            // Step 2
            var response2 = await ChatAsync("Who uses it?", sessionId);
            var json2 = JsonDocument.Parse(response2);
            var result2 = json2.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: "It" resolves to AIOS, context retained.
            Assert.True(result2.Contains("school") || result2.Contains("student") || result2.Contains("admin"));
        }

        [Fact]
        public async Task TCMEM002_Bookreview_EntityTracking()
        {
            string sessionId = Guid.NewGuid().ToString();
            
            // Step 1
            await ChatAsync("Tell me about Bookreview report.docx.", sessionId);
            
            // Step 2
            var response2 = await ChatAsync("Summarize it.", sessionId);
            var json2 = JsonDocument.Parse(response2);
            var result2 = json2.RootElement.GetProperty("result").GetString()?.ToLower();
            
            // Expected: "It" resolves correctly to book review / deep work
            Assert.True(result2.Contains("deep work") || result2.Contains("cal newport") || result2.Contains("focus"));
        }
    }
}
