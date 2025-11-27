namespace FSH.Framework.Core.Domain;
/// <summary>Base domain event with correlation and tenant context.</summary>
public abstract record DomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    string? CorrelationId = null,
    string? TenantId = null
) : IDomainEvent
{
    public static T Create<T>(Func<Guid, DateTimeOffset, T> factory)
        where T : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(factory);
        return factory(Guid.NewGuid(), DateTimeOffset.UtcNow);
    }
}
