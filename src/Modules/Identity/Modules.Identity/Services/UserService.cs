using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Caching;
using FSH.Framework.Core.Common;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Eventing.Outbox;
using FSH.Framework.Jobs.Services;
using FSH.Framework.Mailing;
using FSH.Framework.Mailing.Services;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Storage;
using FSH.Framework.Storage.DTOs;
using FSH.Framework.Storage.Services;
using FSH.Modules.Identity.Contracts.DTOs;
using FSH.Modules.Identity.Contracts.Events;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Data;
using FSH.Modules.Identity.Features.v1.Roles;
using FSH.Modules.Identity.Features.v1.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Text;

namespace FSH.Framework.Infrastructure.Identity.Users.Services;

internal sealed partial class UserService(
    UserManager<FshUser> userManager,
    SignInManager<FshUser> signInManager,
    RoleManager<FshRole> roleManager,
    IdentityDbContext db,
    ICacheService cache,
    IJobService jobService,
    IMailService mailService,
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    IStorageService storageService,
    IOutboxStore outboxStore
    ) : IUserService
{
    private void EnsureValidTenant()
    {
        if (string.IsNullOrWhiteSpace(multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id))
        {
            throw new UnauthorizedException("invalid tenant");
        }
    }

    public async Task<string> ConfirmEmailAsync(string userId, string code, string tenant, CancellationToken cancellationToken)
    {
        EnsureValidTenant();

        var user = await userManager.Users
            .Where(u => u.Id == userId && !u.EmailConfirmed)
            .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new CustomException("An error occurred while confirming E-Mail.");

        code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await userManager.ConfirmEmailAsync(user, code);

        return result.Succeeded
            ? string.Format("Account Confirmed for E-Mail {0}. You can now use the /api/tokens endpoint to generate JWT.", user.Email)
            : throw new CustomException(string.Format("An error occurred while confirming {0}", user.Email));
    }

    public Task<string> ConfirmPhoneNumberAsync(string userId, string code)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> ExistsWithEmailAsync(string email, string? exceptId = null)
    {
        EnsureValidTenant();
        return await userManager.FindByEmailAsync(email.Normalize()) is FshUser user && user.Id != exceptId;
    }

    public async Task<bool> ExistsWithNameAsync(string name)
    {
        EnsureValidTenant();
        return await userManager.FindByNameAsync(name) is not null;
    }

    public async Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, string? exceptId = null)
    {
        EnsureValidTenant();
        return await userManager.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber) is FshUser user && user.Id != exceptId;
    }

    public async Task<UserDto> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new NotFoundException("user not found");

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            ImageUrl = user.ImageUrl?.ToString(),
            IsActive = user.IsActive
        };
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken) =>
        userManager.Users.AsNoTracking().CountAsync(cancellationToken);

    public async Task<List<UserDto>> GetListAsync(CancellationToken cancellationToken)
    {
        var users = await userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        var result = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            result.Add(new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ImageUrl = user.ImageUrl?.ToString(),
                IsActive = user.IsActive
            });
        }

        return result;
    }

    public Task<string> GetOrCreateFromPrincipalAsync(ClaimsPrincipal principal)
    {
        throw new NotImplementedException();
    }

    public async Task<string> RegisterAsync(string firstName, string lastName, string email, string userName, string password, string confirmPassword, string phoneNumber, string origin, CancellationToken cancellationToken)
    {
        if (password != confirmPassword) throw new CustomException("password mismatch.");

        // create user entity
        var user = new FshUser
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            UserName = userName,
            PhoneNumber = phoneNumber,
            IsActive = true,
            EmailConfirmed = false,
            PhoneNumberConfirmed = false,
        };

        // register user
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(error => error.Description).ToList();
            throw new CustomException("error while registering a new user", errors);
        }

        // add basic role
        await userManager.AddToRoleAsync(user, RoleConstants.Basic);

        // send confirmation mail
        if (!string.IsNullOrEmpty(user.Email))
        {
            string emailVerificationUri = await GetEmailVerificationUriAsync(user, origin);
            var mailRequest = new MailRequest(
                new Collection<string> { user.Email },
                "Confirm Registration",
                emailVerificationUri);
            jobService.Enqueue("email", () => mailService.SendAsync(mailRequest, cancellationToken));
        }

        // enqueue integration event for user registration
        var tenantId = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id;
        var correlationId = Guid.NewGuid().ToString();
        var integrationEvent = new UserRegisteredIntegrationEvent(
            Id: Guid.NewGuid(),
            OccurredOnUtc: DateTime.UtcNow,
            TenantId: tenantId,
            CorrelationId: correlationId,
            Source: "Identity",
            UserId: user.Id,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName ?? string.Empty,
            LastName: user.LastName ?? string.Empty);

        await outboxStore.AddAsync(integrationEvent, cancellationToken).ConfigureAwait(false);

        return user.Id;
    }

    public async Task ToggleStatusAsync(bool activateUser, string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.Where(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new NotFoundException("User Not Found.");

        bool isAdmin = await userManager.IsInRoleAsync(user, RoleConstants.Admin);
        if (isAdmin)
        {
            throw new CustomException("Administrators Profile's Status cannot be toggled");
        }

        user.IsActive = activateUser;

        await userManager.UpdateAsync(user);
    }

    public async Task UpdateAsync(string userId, string firstName, string lastName, string phoneNumber, FileUploadRequest image, bool deleteCurrentImage)
    {
        var user = await userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("user not found");

        Uri imageUri = user.ImageUrl ?? null!;
        if (image.Data != null || deleteCurrentImage)
        {
            var imageString = await storageService.UploadAsync<FshUser>(image, FileType.Image);
            user.ImageUrl = new Uri(imageString);
            if (deleteCurrentImage && imageUri != null)
            {
                await storageService.RemoveAsync(imageUri.ToString());
            }
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        string? currentPhoneNumber = await userManager.GetPhoneNumberAsync(user);
        if (phoneNumber != currentPhoneNumber)
        {
            await userManager.SetPhoneNumberAsync(user, phoneNumber);
        }

        var result = await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);

        if (!result.Succeeded)
        {
            throw new CustomException("Update profile failed");
        }
    }

    public async Task DeleteAsync(string userId)
    {
        FshUser? user = await userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("User Not Found.");

        user.IsActive = false;
        IdentityResult? result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            List<string> errors = result.Errors.Select(error => error.Description).ToList();
            throw new CustomException("Delete profile failed", errors);
        }
    }

    private async Task<string> GetEmailVerificationUriAsync(FshUser user, string origin)
    {
        EnsureValidTenant();

        string code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        const string route = "api/users/confirm-email/";
        var endpointUri = new Uri(string.Concat($"{origin}/", route));
        string verificationUri = QueryHelpers.AddQueryString(endpointUri.ToString(), QueryStringKeys.UserId, user.Id);
        verificationUri = QueryHelpers.AddQueryString(verificationUri, QueryStringKeys.Code, code);
        verificationUri = QueryHelpers.AddQueryString(verificationUri,
            MultitenancyConstants.Identifier,
            multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id!);
        return verificationUri;
    }

    public async Task<string> AssignRolesAsync(string userId, List<UserRoleDto> userRoles, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.Where(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new NotFoundException("user not found");

        // Check if the user is an admin for which the admin role is getting disabled
        if (await userManager.IsInRoleAsync(user, RoleConstants.Admin)
            && userRoles.Exists(a => !a.Enabled && a.RoleName == RoleConstants.Admin))
        {
            // Get count of users in Admin Role
            int adminCount = (await userManager.GetUsersInRoleAsync(RoleConstants.Admin)).Count;

            // Check if user is not Root Tenant Admin
            // Edge Case : there are chances for other tenants to have users with the same email as that of Root Tenant Admin. Probably can add a check while User Registration
            if (user.Email == MultitenancyConstants.Root.EmailAddress)
            {
                if (multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id == MultitenancyConstants.Root.Id)
                {
                    throw new CustomException("action not permitted");
                }
            }
            else if (adminCount <= 2)
            {
                throw new CustomException("tenant should have at least 2 admins.");
            }
        }

        foreach (var userRole in userRoles)
        {
            // Check if Role Exists
            if (await roleManager.FindByNameAsync(userRole.RoleName!) is not null)
            {
                if (userRole.Enabled)
                {
                    if (!await userManager.IsInRoleAsync(user, userRole.RoleName!))
                    {
                        await userManager.AddToRoleAsync(user, userRole.RoleName!);
                    }
                }
                else
                {
                    await userManager.RemoveFromRoleAsync(user, userRole.RoleName!);
                }
            }
        }

        return "User Roles Updated Successfully.";

    }

    public async Task<List<UserRoleDto>> GetUserRolesAsync(string userId, CancellationToken cancellationToken)
    {
        var userRoles = new List<UserRoleDto>();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) throw new NotFoundException("user not found");
        var roles = await roleManager.Roles.AsNoTracking().ToListAsync(cancellationToken);
        if (roles is null) throw new NotFoundException("roles not found");
        foreach (var role in roles)
        {
            userRoles.Add(new UserRoleDto
            {
                RoleId = role.Id,
                RoleName = role.Name,
                Description = role.Description,
                Enabled = await userManager.IsInRoleAsync(user, role.Name!)
            });
        }

        return userRoles;
    }
}
