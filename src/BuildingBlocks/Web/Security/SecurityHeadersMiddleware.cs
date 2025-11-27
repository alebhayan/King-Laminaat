using Microsoft.AspNetCore.Http;

namespace FSH.Framework.Web.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var path = context.Request.Path;

        // Allow OpenAPI / Scalar UI to manage their own scripts/styles.
        if (path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase))
        {
            return next(context);
        }

        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-XSS-Protection"] = "0";

        if (!headers.ContainsKey("Content-Security-Policy"))
        {
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "img-src 'self' data: https:; " +
                "script-src 'self' https:; " +
                "style-src 'self' 'unsafe-inline'; " +
                "object-src 'none'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self';";
        }

        return next(context);
    }
}
