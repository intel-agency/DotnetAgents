namespace DotnetAgents.Core.Models;

public record TaskStatusChangedPayload(
    Guid TaskId,
    string Status,
    string? Result,
    string? ErrorMessage,
    int CurrentIteration,
    int MaxIterations,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    double? DurationSeconds,
    double? ElapsedSeconds
);

public record TaskProgressPayload(
    Guid TaskId,
    int CurrentIteration,
    int MaxIterations,
    string Message,
    DateTimeOffset Timestamp
);

public record TaskStartedPayload(
    Guid TaskId,
    DateTimeOffset StartedAt
);

public record TaskCompletedPayload(
    Guid TaskId,
    string? Result,
    string? ErrorMessage,
    DateTimeOffset CompletedAt
);
