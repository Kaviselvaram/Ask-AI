using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using backend.Models;

namespace backend.Services;

public interface IWorkspaceCatalogBuilder
{
    Task<List<string>> GetAvailableDocumentsAsync(string connectionString);
}
