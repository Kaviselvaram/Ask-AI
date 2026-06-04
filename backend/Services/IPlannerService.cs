using System.Threading.Tasks;
using backend.Models;

namespace backend.Services
{
    public interface IPlannerService
    {
        Task<ExecutionPlan> CreatePlanAsync(string query, QueryIntent intent);
    }
}
