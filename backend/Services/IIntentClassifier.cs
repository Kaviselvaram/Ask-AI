using System.Threading.Tasks;
using backend.Models;

namespace backend.Services
{
    public interface IIntentClassifier
    {
        Task<QueryIntent> ClassifyAsync(string query);
    }
}
