using FSH.Modules.Auditing.Contracts;

namespace FSH.Modules.Auditing.Contracts.Dtos;

public sealed class AuditSummaryAggregateDto
{
    public IDictionary<AuditEventType, long> EventsByType { get; set; } =
        new Dictionary<AuditEventType, long>();

    public IDictionary<AuditSeverity, long> EventsBySeverity { get; set; } =
        new Dictionary<AuditSeverity, long>();

    public IDictionary<string, long> EventsBySource { get; set; } =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, long> EventsByTenant { get; set; } =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
}

