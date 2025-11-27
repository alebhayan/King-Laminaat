using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace FSH.Playground.Blazor.Services;

public sealed record BffTokenResponse(
    string AccessToken,
    string RefreshToken,
    System.DateTime RefreshTokenExpiresAt,
    System.DateTime AccessTokenExpiresAt);

public interface ITokenStore
{
    Task StoreAsync(string subject, BffTokenResponse token, CancellationToken cancellationToken = default);
    Task<BffTokenResponse?> GetAsync(string subject, CancellationToken cancellationToken = default);
    Task RemoveAsync(string subject, CancellationToken cancellationToken = default);
}

public sealed class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<string, BffTokenResponse> _tokens = new();

    public Task StoreAsync(string subject, BffTokenResponse token, CancellationToken cancellationToken = default)
    {
        _tokens[subject] = token;
        return Task.CompletedTask;
    }

    public Task<BffTokenResponse?> GetAsync(string subject, CancellationToken cancellationToken = default)
    {
        _tokens.TryGetValue(subject, out var token);
        return Task.FromResult(token);
    }

    public Task RemoveAsync(string subject, CancellationToken cancellationToken = default)
    {
        _tokens.TryRemove(subject, out _);
        return Task.CompletedTask;
    }
}

public sealed class BffAuthDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITokenStore _tokenStore;

    private const string SessionCookieName = "fsh_session_id";

    public BffAuthDelegatingHandler(IHttpContextAccessor httpContextAccessor, ITokenStore tokenStore)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenStore = tokenStore;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var sessionId = httpContext?.Request.Cookies[SessionCookieName];

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var token = await _tokenStore.GetAsync(sessionId, cancellationToken);
            if (token is not null && !string.IsNullOrWhiteSpace(token.AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

                if (!request.Headers.Contains("tenant"))
                {
                    var tenant = httpContext?.Request.Cookies["fsh_tenant"] ?? "root";
                    request.Headers.TryAddWithoutValidation("tenant", tenant);
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

public static class BffAuthEndpoints
{
    public static void MapBffAuthEndpoints(this WebApplication app)
    {
        const string SessionCookieName = "fsh_session_id";
        const string TenantCookieName = "fsh_tenant";

        app.MapPost("/auth/login", async (
            LoginRequest request,
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            ITokenStore tokenStore,
            CancellationToken cancellationToken) =>
        {
            var client = httpClientFactory.CreateClient("AuthApi");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/token")
            {
                Content = JsonContent.Create(new { request.Email, request.Password })
            };

            var tenant = string.IsNullOrWhiteSpace(request.Tenant) ? "root" : request.Tenant;
            httpRequest.Headers.TryAddWithoutValidation("tenant", tenant);

            var response = await client.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Results.Unauthorized();
            }

            var token = await response.Content.ReadFromJsonAsync<BffTokenResponse>(cancellationToken: cancellationToken);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return Results.Problem("Invalid token response from identity API.");
            }

            var sessionId = Guid.NewGuid().ToString("N");
            await tokenStore.StoreAsync(sessionId, token, cancellationToken);

            var isHttps = httpContext.Request.IsHttps;

            httpContext.Response.Cookies.Append(
                SessionCookieName,
                sessionId,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = isHttps,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });

            httpContext.Response.Cookies.Append(
                TenantCookieName,
                tenant,
                new CookieOptions
                {
                    HttpOnly = false,
                    Secure = isHttps,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });

            return Results.Ok();
        });

        app.MapPost("/auth/logout", async (
            HttpContext httpContext,
            ITokenStore tokenStore,
            CancellationToken cancellationToken) =>
        {
            var sessionId = httpContext.Request.Cookies[SessionCookieName];
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                await tokenStore.RemoveAsync(sessionId, cancellationToken);
            }

            httpContext.Response.Cookies.Delete(SessionCookieName);
            httpContext.Response.Cookies.Delete(TenantCookieName);

            return Results.Ok();
        });

        app.MapGet("/auth/status", async (
            HttpContext httpContext,
            ITokenStore tokenStore,
            CancellationToken cancellationToken) =>
        {
            var sessionId = httpContext.Request.Cookies[SessionCookieName];
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Results.Unauthorized();
            }

            var token = await tokenStore.GetAsync(sessionId, cancellationToken);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return Results.Unauthorized();
            }

            return Results.Ok();
        });
    }
}

public sealed record LoginRequest(string Email, string Password, string? Tenant);
