namespace FSH.Framework.Core.Domain;
public abstract class BaseEntity<TId> : IEntity<TId>, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; protected set; } = default!;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>Raise and record a domain event for later dispatch.</summary>
    protected void AddDomainEvent(IDomainEvent @event)
        => _domainEvents.Add(@event);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
