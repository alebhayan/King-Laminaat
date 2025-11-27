using FSH.Framework.Core.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FSH.Framework.Persistence.Inteceptors;

public sealed class DomainEventsInterceptor : SaveChangesInterceptor
{
    private readonly IPublisher _publisher;
    private readonly ILogger<DomainEventsInterceptor> _logger;

    public DomainEventsInterceptor(IPublisher publisher, ILogger<DomainEventsInterceptor> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        var context = eventData.Context;
        if (context == null)
            return await base.SavedChangesAsync(eventData, result, cancellationToken);

        var domainEvents = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .SelectMany(e =>
            {
                var pending = e.Entity.DomainEvents.ToArray();
                e.Entity.ClearDomainEvents();
                return pending;
            })
            .ToArray();

        if (domainEvents.Length == 0)
            return await base.SavedChangesAsync(eventData, result, cancellationToken);

        _logger.LogDebug("Publishing {Count} domain events...", domainEvents.Length);

        foreach (var domainEvent in domainEvents)
            await _publisher.Publish(domainEvent, cancellationToken).ConfigureAwait(false);

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}