using FSH.Modules.Auditing.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Auditing.Persistence;

/// <summary>
/// Persists audit envelopes into SQL using EF Core.
/// </summary>
public sealed class SqlAuditSink : IAuditSink
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuditSerializer _serializer;
    private readonly ILogger<SqlAuditSink> _log;

    public SqlAuditSink(IServiceScopeFactory scopeFactory, IAuditSerializer serializer, ILogger<SqlAuditSink> log)
        => (_scopeFactory, _serializer, _log) = (scopeFactory, serializer, log);

    public async Task WriteAsync(IReadOnlyList<AuditEnvelope> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        var records = batch.Select(e => new AuditRecord
        {
            Id = e.Id,
            OccurredAtUtc = e.OccurredAtUtc,
            ReceivedAtUtc = e.ReceivedAtUtc,
            EventType = (int)e.EventType,
            Severity = (byte)e.Severity,
            TenantId = e.TenantId,
            UserId = e.UserId,
            UserName = e.UserName,
            TraceId = e.TraceId,
            SpanId = e.SpanId,
            CorrelationId = e.CorrelationId,
            RequestId = e.RequestId,
            Source = e.Source,
            Tags = (long)e.Tags,
            PayloadJson = _serializer.SerializePayload(e.Payload)
        }).ToList();

        db.AuditRecords.AddRange(records);
        await db.SaveChangesAsync(ct);

        _log.LogInformation("Wrote {Count} audit records.", records.Count);
    }
}
