using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotnetAgents.Core.Interfaces
{
    public interface IToolDispatcher
    {
        List<string> GetAllToolSchemas();
        Task<string> DispatchAsync(string toolName, string jsonArguments);
    }
}