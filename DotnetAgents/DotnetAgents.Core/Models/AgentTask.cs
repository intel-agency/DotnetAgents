using System;

namespace DotnetAgents.Core.Models
{
    /// <summary>
    /// Represents a single, long-running task for an agent.
    /// This is the record stored in the database to track job status.
    /// </summary>
    public class AgentTask
    {
        public Guid Id { get; set; }
        public string? Goal { get; set; } = string.Empty;     
        public Status Status { get; set; } // e.g., "Queued", "Running", "Thinking", "Completed", "Failed"
        public string? CreatedByUserId { get; set; }        
    }
}