# Eventing Building Block – Design Spec

This spec describes the **Eventing** building block for the FSH .NET 10 framework. It captures the requirements we discussed and the design that other modules (starting with Identity) will build on.

---

## 1. Goals & Non-Goals

### Goals

- Provide a **standard, reusable eventing abstraction** that:
  - Supports both **domain events** (already present) and **integration events** (new).
  - Works in **single-process** modular apps and **multi-service** deployments.
  - Supports **multiple event bus providers** behind a common interface.
- Implement the **Outbox pattern** for reliable publishing:
  - Ensure integration events are only published if the local transaction commits.
  - Avoid “lost” events on failures.
- Provide an **Inbox pattern** for idempotent consumers:
  - Allow safe at-least-once delivery semantics from the bus.
  - Prevent duplicate processing.
- Make **TenantId** a first-class concept for events:
  - All integration events carry TenantId metadata (nullable to allow global events).
- Use **Hangfire** as a first implementation for the outbox dispatcher in process:
  - Later, still compatible with external workers.
- Integrate cleanly with existing modules, starting with Identity:
  - Example: `UserRegisteredIntegrationEvent` published by Identity, handled in Identity to send a welcome email.

### Non-Goals (for initial version)

- Implement concrete external providers beyond **InMemory**:
  - The abstraction must support them, but only InMemoryEventBus is required initially.
- Provide a complete ES/CQRS framework:
  - We only cover event dispatching and basic outbox/inbox; full event sourcing is out of scope.

---

## 2. Conceptual Model

### 2.1 Domain Events vs Integration Events

- **Domain events**
  - Internal to a module/bounded context.
  - Raised by aggregates/entities to signal something that happened in the domain.
  - Only handled in-process within the same service.
  - Do not cross process boundaries and are not versioned for external consumers.

- **Integration events**
  - Public “API events” exposed by a module to other modules/services.
  - Derived from domain events (often 1:1 or aggregated).
  - Persisted to an outbox and published to an event bus.
  - Carry TenantId and correlation metadata.
  - Versioned informally: breaking changes → new event type; additive changes → new optional properties.

**Rule of thumb:**

- Domain events speak the **domain language**.
- Integration events speak the **integration language** (what others need to know).

---

## 3. Building Block Layout

New project:

- `src/BuildingBlocks/Eventing/Eventing.csproj`

Proposed namespaces:

- `FSH.Framework.Eventing`
  - Public abstractions (interfaces, base types).
- `FSH.Framework.Eventing.Outbox`
  - Outbox/inbox entities and services.
- `FSH.Framework.Eventing.InMemory`
  - In-memory event bus implementation (initial provider).

---

## 4. Abstractions

### 4.1 IIntegrationEvent

Base interface (or abstract record) for all integration events:

- Required properties:
  - `Guid Id` – event identifier (used for idempotency).
  - `DateTime OccurredOnUtc` – when the event occurred.
  - `string? TenantId` – current tenant; null for global events.
  - `string CorrelationId` – correlation ID (from request or generated).
  - `string Source` – module/service that produced the event (e.g., `"Identity"`, `"Multitenancy"`).

Modules define their own events in their Contracts project by implementing `IIntegrationEvent`.

### 4.2 IEventBus

Abstraction over any event bus:

```csharp
public interface IEventBus
{
    Task PublishAsync(IIntegrationEvent @event, CancellationToken ct = default);
    Task PublishAsync(IEnumerable<IIntegrationEvent> events, CancellationToken ct = default);
}
```

- Initial provider: InMemory (in-process pub/sub).
- Future providers: RabbitMQ, Azure Service Bus, Kafka, etc.

### 4.3 IIntegrationEventHandler<TEvent>

Consumer-side abstraction:

```csharp
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
```

- Implemented in module implementation projects (e.g., `Modules.Identity`).
- Registered in DI; Eventing resolves and calls them through the bus or an inbox wrapper.

### 4.4 IEventSerializer

Responsible for turning integration events into payloads and back:

```csharp
public interface IEventSerializer
{
    string Serialize(IIntegrationEvent @event);
    IIntegrationEvent? Deserialize(string payload, string eventTypeName);
}
```

- First implementation uses `System.Text.Json`.
- `eventTypeName` will be the assembly-qualified name or a configured mapping.

---

## 5. Outbox Pattern

### 5.1 OutboxMessage Entity

An `OutboxMessage` EF entity added to DbContexts that want eventing (e.g., IdentityDbContext):

- Properties:
  - `Guid Id`
  - `DateTime CreatedOnUtc`
  - `string Type` – CLR type name (e.g., `"FSH.Modules.Identity.Contracts.Events.UserRegisteredIntegrationEvent, Modules.Identity.Contracts"`).
  - `string Payload` – serialized JSON.
  - `string? TenantId`
  - `string? CorrelationId`
  - `DateTime? ProcessedOnUtc`
  - `int RetryCount`
  - `string? LastError`
  - `bool IsDead` – whether the message has been moved to a “dead” state after too many failures.

### 5.2 IOutboxStore

Service abstraction for writing/reading outbox messages:

```csharp
public interface IOutboxStore
{
    Task AddAsync(IIntegrationEvent @event, CancellationToken ct = default);
    Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(int batchSize, CancellationToken ct = default);
    Task MarkAsProcessedAsync(OutboxMessage message, CancellationToken ct = default);
    Task MarkAsFailedAsync(OutboxMessage message, string error, bool isDead, CancellationToken ct = default);
}
```

Characteristics:

- `AddAsync` must be called from within the same DbContext transaction as domain changes.
- `GetPendingBatchAsync` selects unprocessed messages ordered by creation time.

### 5.3 Dispatching (Hangfire Job)

An `OutboxDispatcherJob` class that:

1. Reads a batch of pending messages via `IOutboxStore`.
2. For each:
   - Deserializes to `IIntegrationEvent` using `IEventSerializer`.
   - Publishes via `IEventBus`.
   - On success:
     - `MarkAsProcessedAsync`.
   - On failure:
     - Increment `RetryCount`, record `LastError`, and if `RetryCount >= MaxRetries`, mark `IsDead = true`.
     - Optionally emit an audit/exception event through the Auditing module.

Configuration:

- `EventingOptions.OutboxBatchSize` (e.g., 100).
- `EventingOptions.OutboxMaxRetries` (e.g., 5).
- Execution:
  - Registered as a **recurring Hangfire job** (e.g., every 10 seconds) when Jobs are enabled.

### 5.4 Failure Handling

Per requirements:

- After exceeding `OutboxMaxRetries`, messages are marked as **dead** (`IsDead = true`) and no longer retried.
- We should:
  - Emit a warning log.
  - Optionally write a security/exception audit for visibility.

---

## 6. Inbox Pattern (Idempotent Consumers)

### 6.1 InboxMessage Entity

An `InboxMessage` entity to track processed integration events per handler:

- Properties:
  - `Guid Id` – event Id.
  - `string EventType` – event CLR type name.
  - `string HandlerName` – handler id (e.g., full type name).
  - `DateTime ProcessedOnUtc`
  - `string? TenantId`

### 6.2 IInboxStore

Service abstraction:

```csharp
public interface IInboxStore
{
    Task<bool> HasProcessedAsync(Guid eventId, string handlerName, CancellationToken ct = default);
    Task MarkProcessedAsync(Guid eventId, string handlerName, string? tenantId, CancellationToken ct = default);
}
```

### 6.3 Idempotent Handler Decorator

Infrastructure that wraps `IIntegrationEventHandler<TEvent>`:

- Pseudocode:

```csharp
public sealed class IdempotentIntegrationEventHandler<TEvent> : IIntegrationEventHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    private readonly IIntegrationEventHandler<TEvent> _inner;
    private readonly IInboxStore _inbox;
    private readonly string _handlerName;

    public async Task HandleAsync(TEvent @event, CancellationToken ct = default)
    {
        if (await _inbox.HasProcessedAsync(@event.Id, _handlerName, ct))
            return;

        await _inner.HandleAsync(@event, ct);
        await _inbox.MarkProcessedAsync(@event.Id, _handlerName, @event.TenantId, ct);
    }
}
```

- Registered via DI so all handlers can be decorated automatically.

---

## 7. InMemory Event Bus

### 7.1 InMemoryEventBus Implementation

Initial provider that works in single-process deployments:

- Maintains a mapping:
  - `Dictionary<Type, List<Func<IIntegrationEvent, CancellationToken, Task>>>`
- `PublishAsync`:
  - Looks up handlers for the event type.
  - For each handler:
    - Resolves `IIntegrationEventHandler<T>` from DI.
    - Wraps with `IdempotentIntegrationEventHandler<T>` if inbox is enabled.
    - Calls `HandleAsync`.

Usage:

- Configured by default when `AddEventing()` is called with provider `"InMemory"` or no provider specified.
- Suitable for the current single-process modular app.
- Later, external providers can be swapped in with the same `IEventBus` interface.

---

## 8. Module Integration (Example: Identity)

### 8.1 Event Definition

In `Modules.Identity.Contracts`:

- Folder: `Events/`
- Example:

```csharp
public sealed record UserRegisteredIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    string UserId,
    string Email,
    string FirstName,
    string LastName)
    : IIntegrationEvent;
```

### 8.2 Publishing from Identity

When a user registers successfully:

- A **domain event** is raised (if not already present) such as `UserRegisteredDomainEvent`.
- A domain-event handler (or an application service) maps:
  - Domain event → `UserRegisteredIntegrationEvent`.
- It obtains `IOutboxStore` from DI and calls:

```csharp
await _outboxStore.AddAsync(new UserRegisteredIntegrationEvent(
    Id: Guid.NewGuid(),
    OccurredOnUtc: DateTime.UtcNow,
    TenantId: currentTenant.Id,
    CorrelationId: correlationId,
    Source: "Identity",
    UserId: user.Id,
    Email: user.Email!,
    FirstName: user.FirstName ?? string.Empty,
    LastName: user.LastName ?? string.Empty), ct);
```

This call is made within the same transaction as user creation so outbox and user changes are atomic.

### 8.3 Handling in Identity (Welcome Email)

In `Modules.Identity` implementation project:

- Handler:

```csharp
public sealed class UserRegisteredEmailHandler
    : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly IMailService _mailService;

    public UserRegisteredEmailHandler(IMailService mailService)
        => _mailService = mailService;

    public async Task HandleAsync(UserRegisteredIntegrationEvent @event, CancellationToken ct = default)
    {
        var mail = new MailRequest
        {
            To = @event.Email,
            Subject = "Welcome!",
            Body = $"Hi {@event.FirstName}, thanks for registering."
        };

        await _mailService.SendAsync(mail, ct);
    }
}
```

Eventing:

- InMemoryEventBus resolves this handler and executes it when the outbox dispatcher publishes the event.
- Inbox wrapper ensures idempotency if the same event is delivered multiple times.

---

## 9. Configuration & Wiring

### 9.1 EventingOptions

Configuration object:

- `string Provider` – `"InMemory"` by default.
- `int OutboxBatchSize` – default 100.
- `int OutboxMaxRetries` – default 5.
- `TimeSpan OutboxPollingInterval` – interval for Hangfire job (if needed).
- `bool EnableInbox` – default true.

Bound from configuration section, e.g. `EventingOptions`.

### 9.2 Service Registration

In a central place (likely BuildingBlocks.Web Extensions or Eventing.Extensions):

- `AddEventing(this IServiceCollection services, Action<EventingOptions> configure)`
  - Registers:
    - `IEventSerializer`
    - `IEventBus` (InMemory or other based on options.Provider)
    - `IOutboxStore` and `IInboxStore` (per-DbContext or generic).
  - Optionally extension for `AddOutbox<TDbContext>()` that:
    - Registers entity.
    - Adds appropriate DbContext configuration.
- `AddIntegrationEventHandlers(Assembly[] assemblies)`
  - Scans for `IIntegrationEventHandler<T>` and registers them.

### 9.3 Outbox Dispatcher Job

- When Jobs are enabled via `AddHeroPlatform(o => o.EnableJobs = true)`:
  - Register a recurring Hangfire job:
    - Name: `"eventing-outbox-dispatcher"`.
    - Target: `OutboxDispatcherJob.RunAsync()`.
    - Schedule: e.g., `*/10 * * * * *` (every 10 seconds) or configurable.

---

## 10. Open Issues / Future Enhancements

1. **External Providers**
   - Later add `RabbitMqEventBus`, `AzureServiceBusEventBus`, etc.
   - Might need:
     - Conventions for topic/exchange names (e.g., module-based).
     - Dead-letter queue handling.

2. **Event Contracts Organization**
   - May want a dedicated `Modules.<Name>.Contracts.Events` namespace in each module.
   - Consider tooling/docs to ensure integration events are documented (similar to HTTP endpoints).

3. **Correlation with Observability**
   - Integrate event Id and CorrelationId with OpenTelemetry:
     - Add spans for event publish/handle.
     - Propagate trace context via event headers where applicable.

4. **Administrative Tools**
   - Simple endpoints or diagnostics to:
     - Inspect dead outbox messages.
     - Requeue or manually mark them processed.
     - Inspect inbox state for debugging.

5. **Security**
   - For external event buses, ensure:
     - TLS, authentication, and authorization are configurable.
     - No sensitive data is placed in event payloads without masking.

This spec has been implemented as:

- Building block:
  - `src/BuildingBlocks/Eventing/*`
  - Includes `IIntegrationEvent`, `IEventBus`, in-memory bus, outbox/inbox, dispatcher, and DI extensions.
- Identity integration:
  - Eventing wired in `IdentityModule.ConfigureServices`.
  - `IdentityDbContext` exposes `OutboxMessages` and `InboxMessages` and applies eventing configurations.
  - `UserService.RegisterAsync` publishes `UserRegisteredIntegrationEvent` to the outbox via `IOutboxStore`.
  - `UserRegisteredEmailHandler` consumes the integration event via `IIntegrationEventHandler<UserRegisteredIntegrationEvent>` and sends a welcome email.

Further work (external providers, admin tooling, deeper observability) can build on this foundation.
