using System.Threading.Tasks;
using backend.Models;

namespace backend.Agents
{
    public abstract class BaseAgent
    {
        public abstract string AgentName { get; }
        public abstract Task ExecuteAsync(AgentState state);
    }
}
