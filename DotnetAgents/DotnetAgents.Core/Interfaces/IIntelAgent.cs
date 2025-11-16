using DotnetAgents.Core.Models;

using System.Threading;
using System.Threading.Tasks;

namespace DotnetAgents.Core.Interfaces
{
    /// <summary>
    /// Defines the core logic for the intelligent agent.
    /// This interface is implemented by AgentApi.IntelAgent.Agent.
    /// </summary>
    public interface IIntelAgent
    {
        /// <summary>
        /// Executes the main agent loop for a given task.
        /// This method will be called by the AgentWorkerService.
        /// </summary>
        /// <param name="task">The task to execute.</param>
        /// <param name="cancellationToken">A token to stop the loop.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ExecuteTaskAsync(AgentTask task, CancellationToken cancellationToken);
    }
}