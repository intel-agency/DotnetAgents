using System.Threading.Tasks;

namespace DotnetAgents.Core.Interfaces
{
    /// <summary>
    /// A single, stateless tool that an agent can execute.
    /// </summary>
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        string GetJsonSchema();
        Task<string> ExecuteAsync(string jsonArguments);
    }
}