using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Features.v1.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FSH.Modules.Identity.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<FshUser> _userManager;
    private readonly ILogger<IdentityService> _logger;
    private readonly IMultiTenantContextAccessor<AppTenantInfo>? _multiTenantContextAccessor;

    public IdentityService(
        UserManager<FshUser> userManager,
        IMultiTenantContextAccessor<AppTenantInfo>? multiTenantContextAccessor,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager;
        _multiTenantContextAccessor = multiTenantContextAccessor;
        _logger = logger;
    }

    public async Task<(string Subject, IEnumerable<Claim> Claims)?>
        ValidateCredentialsAsync(string email, string password, CancellationToken ct = default)
    {
        var currentTenant = _multiTenantContextAccessor!.MultiTenantContext.TenantInfo;
        if (currentTenant == null) throw new UnauthorizedException();

        if (string.IsNullOrWhiteSpace(currentTenant.Id)
           || await _userManager.FindByEmailAsync(email.Trim().Normalize()) is not { } user
           || !await _userManager.CheckPasswordAsync(user, password))
        {
            throw new UnauthorizedException();
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException("user is deactivated");
        }

        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedException("email not confirmed");
        }

        if (currentTenant.Id != MultitenancyConstants.Root.Id)
        {
            if (!currentTenant.IsActive)
            {
                throw new UnauthorizedException($"tenant {currentTenant.Id} is deactivated");
            }

            if (DateTime.UtcNow > currentTenant.ValidUpto)
            {
                throw new UnauthorizedException($"tenant {currentTenant.Id} validity has expired");
            }
        }

        // Build user claims
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.FirstName ?? string.Empty),
            new(ClaimTypes.MobilePhone, user.PhoneNumber ?? string.Empty),
            new(ClaimConstants.Fullname, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            new(ClaimConstants.Tenant, _multiTenantContextAccessor!.MultiTenantContext.TenantInfo!.Id),
            new(ClaimConstants.ImageUrl, user.ImageUrl == null ? string.Empty : user.ImageUrl.ToString())
        };

        // Add roles as claims
        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        return (user.Id, claims);
    }

    public async Task<(string Subject, IEnumerable<Claim> Claims)?>
        ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var currentTenant = _multiTenantContextAccessor!.MultiTenantContext.TenantInfo;
        if (currentTenant == null) throw new UnauthorizedException();

        if (string.IsNullOrWhiteSpace(currentTenant.Id))
        {
            throw new UnauthorizedException();
        }

        var hashedToken = HashToken(refreshToken);

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == hashedToken, ct);

        if (user is null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            throw new UnauthorizedException("refresh token is invalid or expired");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException("user is deactivated");
        }

        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedException("email not confirmed");
        }

        if (currentTenant.Id != MultitenancyConstants.Root.Id)
        {
            if (!currentTenant.IsActive)
            {
                throw new UnauthorizedException($"tenant {currentTenant.Id} is deactivated");
            }

            if (DateTime.UtcNow > currentTenant.ValidUpto)
            {
                throw new UnauthorizedException($"tenant {currentTenant.Id} validity has expired");
            }
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.FirstName ?? string.Empty),
            new(ClaimTypes.MobilePhone, user.PhoneNumber ?? string.Empty),
            new(ClaimConstants.Fullname, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            new(ClaimConstants.Tenant, _multiTenantContextAccessor!.MultiTenantContext.TenantInfo!.Id),
            new(ClaimConstants.ImageUrl, user.ImageUrl == null ? string.Empty : user.ImageUrl.ToString())
        };

        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        return (user.Id, claims);
    }

    public async Task StoreRefreshTokenAsync(string subject, string refreshToken, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        var currentTenant = _multiTenantContextAccessor!.MultiTenantContext.TenantInfo;
        if (currentTenant == null) throw new UnauthorizedException();

        if (string.IsNullOrWhiteSpace(currentTenant.Id))
        {
            throw new UnauthorizedException();
        }

        var user = await _userManager.FindByIdAsync(subject);

        if (user is null)
        {
            throw new UnauthorizedException("user not found");
        }

        user.RefreshToken = HashToken(refreshToken);
        user.RefreshTokenExpiryTime = expiresAtUtc;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            _logger.LogError("Failed to persist refresh token for user {UserId}: {Errors}", subject, string.Join(", ", result.Errors.Select(e => e.Description)));
            throw new UnauthorizedException("could not persist refresh token");
        }
    }

    private static string HashToken(string token)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
