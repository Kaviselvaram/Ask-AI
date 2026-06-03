using System.Collections.Generic;
using System.Threading.Tasks;
using backend.Agents;
using backend.Models;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;

namespace backend.Tests
{
    public class VerificationAgentTests
    {
        [Fact]
        public async Task VerificationAgent_EmptyEvidence_ReturnsZeroConfidence()
        {
            // Arrange
            // Passing null because the empty evidence branch should return before invoking the kernel
            var agent = new VerificationAgent(null!);
            var state = new AgentState
            {
                OriginalQuery = "What is the policy?",
                Evidence = new List<EvidenceNode>()
            };

            // Act
            await agent.ExecuteAsync(state);

            // Assert
            Assert.Equal(0, state.GlobalConfidenceScore);
        }
    }
}
