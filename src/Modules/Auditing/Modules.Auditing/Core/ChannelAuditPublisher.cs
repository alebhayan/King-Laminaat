using FSH.Modules.Auditing.Contracts;
using System.Threading.Channels;

namespace FSH.Modules.Auditing;

/// <summary>
/// Non-blocking publisher using a bounded channel. Writer is used on request path; reader is drained by a background worker.
/// </summary>
public sealed class ChannelAuditPublisher : IAuditPublisher
{
    private readonly Channel<AuditEnvelope> _channel;
    public IAuditScope CurrentScope { get; }

    public ChannelAuditPublisher(IAuditScope scope, int capacity = 50_000)
    {
        // Drop oldest to keep latency predictable under pressure.
        _channel = Channel.CreateBounded<AuditEnvelope>(new BoundedChannelOptions(capacity)
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        CurrentScope = scope;
    }

    public ValueTask PublishAsync(IAuditEvent auditEvent, CancellationToken ct = default)
    {
        if (auditEvent is not AuditEnvelope env)
        {
            // wrap into an envelope if a custom IAuditEvent was passed (rare)
            env = new AuditEnvelope(
                id: Guid.CreateVersion7(),
                occurredAtUtc: auditEvent.OccurredAtUtc,
                receivedAtUtc: DateTime.UtcNow,
                eventType: auditEvent.EventType,
                severity: auditEvent.Severity,
                tenantId: auditEvent.TenantId,
                userId: auditEvent.UserId,
                userName: auditEvent.UserName,
                traceId: auditEvent.TraceId,
                spanId: auditEvent.SpanId,
                correlationId: auditEvent.CorrelationId,
                requestId: auditEvent.RequestId,
                source: auditEvent.Source,
                tags: auditEvent.Tags,
                payload: auditEvent.Payload);
        }

        return _channel.Writer.TryWrite(env)
            ? ValueTask.CompletedTask
            : ValueTask.FromCanceled(ct); // optional: swallow based on config
    }

    internal ChannelReader<AuditEnvelope> Reader => _channel.Reader;
}

