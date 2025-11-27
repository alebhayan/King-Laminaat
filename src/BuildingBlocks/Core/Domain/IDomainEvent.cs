namespace FSH.Framework.Core.Domain;
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOnUtc { get; }
    string? CorrelationId { get; }
    string? TenantId { get; }
}