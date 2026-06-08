using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.EndToEnd.Section10_FullPlatformCertification
{
    public class FullPlatformTests : E2ETestBase
    {
        [Fact]
        public async Task TCE2E001_SequentialExecution()
        {
            string sessionId = Guid.NewGuid().ToString();

            // 1. What is AIOS?
            var r1 = await ChatAsync("What is AIOS?", sessionId);
            Assert.Contains("artificial intelligence", r1.ToLower());

            // 2. Who uses it?
            var r2 = await ChatAsync("Who uses it?", sessionId);
            Assert.True(r2.ToLower().Contains("school") || r2.ToLower().Contains("student") || r2.ToLower().Contains("admin"));

            // 3. Compare it with Bookreview report.txt.
            var r3 = await ChatAsync("Compare it with Bookreview report.txt.", sessionId);
            Assert.True(r3.ToLower().Contains("compare") || r3.ToLower().Contains("difference") || r3.ToLower().Contains("aios"));

            // 4. Identify gaps in AIOS.
            var r4 = await ChatAsync("Identify gaps in AIOS.", sessionId);
            Assert.True(r4.ToLower().Contains("gap") || r4.ToLower().Contains("missing"));

            // 5. Analyze AIOS risks.
            var r5 = await ChatAsync("Analyze AIOS risks.", sessionId);
            Assert.True(r5.ToLower().Contains("risk"));

            // 6. Provide strategic recommendations.
            var r6 = await ChatAsync("Provide strategic recommendations.", sessionId);
            Assert.True(r6.ToLower().Contains("recommendation") || r6.ToLower().Contains("strategic"));
        }
    }
}
