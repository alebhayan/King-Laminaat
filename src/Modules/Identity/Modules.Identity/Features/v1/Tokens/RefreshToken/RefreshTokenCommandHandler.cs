using FSH.Modules.Auditing.Contracts;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Contracts.v1.Tokens.RefreshToken;
using Mediator;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FSH.Modules.Identity.Features.v1.Tokens.RefreshToken;

public sealed class RefreshTokenCommandHandler
    : ICommandHandler<RefreshTokenCommand, RefreshTokenCommandResponse>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly ISecurityAudit _securityAudit;
    private readonly IHttpContextAccessor _http;

    public RefreshTokenCommandHandler(
        IIdentityService identityService,
        ITokenService tokenService,
        ISecurityAudit securityAudit,
        IHttpContextAccessor http)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _securityAudit = securityAudit;
        _http = http;
    }

    public async ValueTask<RefreshTokenCommandResponse> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var http = _http.HttpContext;
        var ip = http?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var ua = http?.Request.Headers.UserAgent.ToString() ?? "unknown";
        var clientId = http?.Request.Headers["X-Client-Id"].ToString();
        if (string.IsNullOrWhiteSpace(clientId)) clientId = "web";

        // Validate refresh token and rebuild subject + claims
        var validated = await _identityService
            .ValidateRefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (validated is null)
        {
            await _securityAudit.TokenRevokedAsync("unknown", clientId!, "InvalidRefreshToken", cancellationToken);
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var (subject, claims) = validated.Value;

        // Optionally, cross-check the provided access token subject
        var handler = new JwtSecurityTokenHandler();
        JwtSecurityToken? parsedAccessToken = null;
        try
        {
            parsedAccessToken = handler.ReadJwtToken(request.Token);
        }
        catch
        {
            // Ignore parsing errors and rely on refresh-token validation
        }

        if (parsedAccessToken is not null)
        {
            var accessTokenSubject = parsedAccessToken.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(accessTokenSubject) &&
                !string.Equals(accessTokenSubject, subject, StringComparison.Ordinal))
            {
                await _securityAudit.TokenRevokedAsync(subject, clientId!, "RefreshTokenSubjectMismatch", cancellationToken);
                throw new UnauthorizedAccessException("Access token subject mismatch.");
            }
        }

        // Audit previous token revocation by rotation (no raw tokens)
        await _securityAudit.TokenRevokedAsync(subject, clientId!, "RefreshTokenRotated", cancellationToken);

        // Issue new tokens
        var newToken = await _tokenService.IssueAsync(subject, claims, null, cancellationToken);

        // Persist rotated refresh token for this user
        await _identityService.StoreRefreshTokenAsync(subject, newToken.RefreshToken, newToken.RefreshTokenExpiresAt, cancellationToken);

        // Audit the newly issued token with a fingerprint
        var fingerprint = Sha256Short(newToken.AccessToken);
        await _securityAudit.TokenIssuedAsync(
            userId: subject,
            userName: claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty,
            clientId: clientId!,
            tokenFingerprint: fingerprint,
            expiresUtc: newToken.AccessTokenExpiresAt,
            ct: cancellationToken);

        return new RefreshTokenCommandResponse(
            Token: newToken.AccessToken,
            RefreshToken: newToken.RefreshToken,
            RefreshTokenExpiryTime: newToken.RefreshTokenExpiresAt);
    }

    private static string Sha256Short(string value)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}

