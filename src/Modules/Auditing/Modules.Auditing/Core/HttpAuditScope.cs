using FSH.Modules.Auditing.Contracts;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Security.Claims;

namespace FSH.Modules.Auditing;

public sealed class HttpAuditScope : IAuditScope
{
    private readonly IHttpContextAccessor _http;
    private readonly ITenantAccessor? _tenant; // your own abstraction; optional

    public HttpAuditScope(IHttpContextAccessor httpContextAccessor, ITenantAccessor? tenantAccessor = null)
        => (_http, _tenant) = (httpContextAccessor, tenantAccessor);

    public string? TenantId => _tenant?.TenantId ?? _http.HttpContext?.Items["TenantId"] as string;
    public string? UserId => _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? _http.HttpContext?.User?.FindFirstValue("sub");
    public string? UserName => _http.HttpContext?.User?.Identity?.Name ?? _http.HttpContext?.User?.FindFirstValue("name");
    public string? TraceId => Activity.Current?.TraceId.ToString();
    public string? SpanId => Activity.Current?.SpanId.ToString();
    public string? CorrelationId => _http.HttpContext?.TraceIdentifier;
    public string? RequestId => _http.HttpContext?.TraceIdentifier;
    public string? Source => _http.HttpContext?.GetEndpoint()?.DisplayName ?? "API";

    public AuditTag Tags => AuditTag.None;

    public IAuditScope WithTags(AuditTag tags) => this; // immutable view
    public IAuditScope WithProperties(string? tenantId = null, string? userId = null, string? userName = null, string? traceId = null,
        string? spanId = null, string? correlationId = null, string? requestId = null, string? source = null, AuditTag? tags = null) => this;
}

public interface ITenantAccessor
{
    string? TenantId { get; }
}

