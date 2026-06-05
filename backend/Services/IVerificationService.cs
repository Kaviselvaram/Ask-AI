using System.Threading;
using System.Threading.Tasks;
using backend.Models;

namespace backend.Services;

public interface IVerificationService
{
    Task<VerificationResult> VerifyAsync(string answer, string retrievedContext, CancellationToken cancellationToken = default);
}
