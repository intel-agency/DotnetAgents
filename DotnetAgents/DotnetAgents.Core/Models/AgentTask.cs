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
        public string? Result { get; set; }
        public string? ErrorMessage { get; set; }

        // Progress tracking
        public int CurrentIteration { get; set; }
        public int MaxIterations { get; set; } = 10;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Last update tracking (for DB POV)
        public DateTime? LastUpdatedAt { get; set; }
        public int UpdateCount { get; set; } = 0;

        // Computed properties
        public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue 
            ? CompletedAt.Value - StartedAt.Value 
            : null;
            
        public TimeSpan? Elapsed => StartedAt.HasValue 
            ? (CompletedAt ?? DateTime.UtcNow) - StartedAt.Value 
            : null;
    }
}