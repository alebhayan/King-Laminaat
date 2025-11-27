namespace FSH.Framework.Eventing;

/// <summary>
/// Configuration options for the eventing building block.
/// </summary>
public sealed class EventingOptions
{
    /// <summary>
    /// Provider for the event bus implementation. Defaults to InMemory.
    /// </summary>
    public string Provider { get; set; } = "InMemory";

    /// <summary>
    /// Batch size for outbox dispatching.
    /// </summary>
    public int OutboxBatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum number of retries before an outbox message is marked as dead.
    /// </summary>
    public int OutboxMaxRetries { get; set; } = 5;

    /// <summary>
    /// Whether inbox-based idempotent handling is enabled.
    /// </summary>
    public bool EnableInbox { get; set; } = true;
}

