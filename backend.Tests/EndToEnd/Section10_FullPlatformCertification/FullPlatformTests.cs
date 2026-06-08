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
            Assert.True(r1.Length > 0);

            // 2. Who uses it?
            var r2 = await ChatAsync("Who uses it?", sessionId);
            Assert.True(r2.Length > 0);

            // 3. Compare it with Bookreview report.pdf.
            var r3 = await ChatAsync("Compare it with Bookreview report.pdf.", sessionId);
            Assert.True(r3.Length > 0);

            // 4. Identify gaps in AIOS.
            var r4 = await ChatAsync("Identify gaps in AIOS.", sessionId);
            Assert.True(r4.Length > 0);

            // 5. Analyze AIOS risks.
            var r5 = await ChatAsync("Analyze AIOS risks.", sessionId);
            Assert.True(r5.Length > 0);

            // 6. Provide strategic recommendations.
            var r6 = await ChatAsync("Provide strategic recommendations.", sessionId);
            Assert.True(r6.Length > 0);
        }
    }
}
