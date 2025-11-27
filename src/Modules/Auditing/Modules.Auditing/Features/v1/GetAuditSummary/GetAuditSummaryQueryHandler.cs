using FSH.Modules.Auditing.Contracts;
using FSH.Modules.Auditing.Contracts.Dtos;
using FSH.Modules.Auditing.Contracts.v1.GetAuditSummary;
using FSH.Modules.Auditing.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Auditing.Features.v1.GetAuditSummary;

public sealed class GetAuditSummaryQueryHandler : IQueryHandler<GetAuditSummaryQuery, AuditSummaryAggregateDto>
{
    private readonly AuditDbContext _dbContext;

    public GetAuditSummaryQueryHandler(AuditDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async ValueTask<AuditSummaryAggregateDto> Handle(GetAuditSummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<AuditRecord> audits = _dbContext.AuditRecords.AsNoTracking();

        if (query.FromUtc.HasValue)
        {
            audits = audits.Where(a => a.OccurredAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            audits = audits.Where(a => a.OccurredAtUtc <= query.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.TenantId))
        {
            audits = audits.Where(a => a.TenantId == query.TenantId);
        }

        var list = await audits.ToListAsync(cancellationToken).ConfigureAwait(false);

        var aggregate = new AuditSummaryAggregateDto();

        foreach (var record in list)
        {
            var type = (AuditEventType)record.EventType;
            aggregate.EventsByType[type] = aggregate.EventsByType.TryGetValue(type, out var c) ? c + 1 : 1;

            var severity = (AuditSeverity)record.Severity;
            aggregate.EventsBySeverity[severity] = aggregate.EventsBySeverity.TryGetValue(severity, out var s) ? s + 1 : 1;

            if (!string.IsNullOrWhiteSpace(record.Source))
            {
                var key = record.Source!;
                aggregate.EventsBySource[key] = aggregate.EventsBySource.TryGetValue(key, out var cs) ? cs + 1 : 1;
            }

            if (!string.IsNullOrWhiteSpace(record.TenantId))
            {
                var tenantKey = record.TenantId!;
                aggregate.EventsByTenant[tenantKey] = aggregate.EventsByTenant.TryGetValue(tenantKey, out var ct) ? ct + 1 : 1;
            }
        }

        return aggregate;
    }
}

