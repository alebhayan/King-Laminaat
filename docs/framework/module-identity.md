# Identity Module

The Identity module provides authentication, authorization, and user management for the framework. It composes ASP.NET Identity, JWT-based tokens, role/permission management, and user CRUD endpoints into a reusable vertical slice.

Namespace root: `FSH.Modules.Identity`  
Implementation root: `src/Modules/Identity/Modules.Identity`  
Contracts root: `src/Modules/Identity/Modules.Identity.Contracts`

---

## Responsibilities

- User and role management with ASP.NET Identity.
- JWT access and refresh tokens for authentication.
- Permissions and authorization policies.
- Multi-tenant identity with Finbuckle.
- Profile management and image storage.
- Audit integration for login and token lifecycle events.
- Eventing integration for publishing and handling integration events.

---

## Architecture

### IdentityModule

File: `src/Modules/Identity/Modules.Identity/IdentityModule.cs`

Implements `IModule`:

- **ConfigureServices**:
  - Registers:
    - `IAuthorizationMiddlewareResultHandler` (`PathAwareAuthorizationHandler`).
    - `ICurrentUser` + `ICurrentUserInitializer` (current user context).
    - `ITokenService` (JWT implementation).
    - `IUserService` / `IRoleService` (user and role application services).
    - `IIdentityService` (credential and refresh-token validation).
    - `IStorageService` (local storage for user images).
    - `IdentityDbContext` via `AddHeroDbContext<IdentityDbContext>()`.
    - `IDbInitializer` implementation: `IdentityDbInitializer`.
    - `IdentityMetrics` for observability.
  - Configures ASP.NET Identity:

    ```csharp
    services.AddIdentity<FshUser, FshRole>(options =>
    {
        options.Password.RequiredLength = IdentityModuleConstants.PasswordLength;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();
    ```

  - Adds health checks:

    ```csharp
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<IdentityDbContext>(
            name: "db:identity",
            failureStatus: HealthStatus.Unhealthy);
    ```

  - Calls `services.ConfigureJwtAuth();` to set up JWT auth (issuer, audience, signing key, etc.).

- **MapEndpoints**:
  - Creates a `api/v1/identity` route group with versioning:
    - `Asp.Versioning` `ApiVersionSet` configured with version 1.
  - Maps endpoints:
    - Tokens
      - `MapGenerateTokenEndpoint()` – `/token`
      - `MapRefreshTokenEndpoint()` – `/token/refresh`
    - Roles
      - `MapGetRolesEndpoint()`
      - `MapGetRoleByIdEndpoint()`
      - `MapDeleteRoleEndpoint()`
      - `MapGetRolePermissionsEndpoint()`
      - `MapUpdateRolePermissionsEndpoint()`
      - `MapCreateOrUpdateRoleEndpoint()`
    - Users
      - `MapAssignUserRolesEndpoint()`
      - `MapChangePasswordEndpoint()`
      - `MapConfirmEmailEndpoint()`
      - `MapDeleteUserEndpoint()`
      - `MapGetUserByIdEndpoint()`
      - `MapGetCurrentUserPermissionsEndpoint()`
      - `MapGetMeEndpoint()`
      - `MapGetUserRolesEndpoint()`
      - `MapGetUsersListEndpoint()`
      - `MapRegisterUserEndpoint()`
      - `MapResetPasswordEndpoint()`
      - `MapSelfRegisterUserEndpoint()`
      - `ToggleUserStatusEndpointEndpoint()`
      - `MapUpdateUserEndpoint()`

  - Token endpoints use the `"auth"` rate-limiting policy:

    ```csharp
    group.MapGenerateTokenEndpoint().AllowAnonymous().RequireRateLimiting("auth");
    group.MapRefreshTokenEndpoint().AllowAnonymous().RequireRateLimiting("auth");
    ```

---

## Persistence

### IdentityDbContext

File: `src/Modules/Identity/Modules.Identity/Data/IdentityDbContext.cs`

- Inherits from `MultiTenantIdentityDbContext<FshUser, FshRole, string, ...>` from Finbuckle.
- Schema configured via `IdentityModuleConstants.SchemaName` (typically `"identity"`).
- Uses `ApplicationUserConfig`, `ApplicationRoleConfig`, etc. (`IdentityConfigurations.cs`) to:
  - Configure table names (`Users`, `Roles`, `UserRoles`, etc.).
  - Enable multi-tenancy through `.IsMultiTenant()`.
  - Adjust unique indexes (per tenant).
- Includes DbSets for eventing:
  - `DbSet<OutboxMessage> OutboxMessages`
  - `DbSet<InboxMessage> InboxMessages`
- Applies eventing configurations:
  - `OutboxMessageConfiguration(IdentityModuleConstants.SchemaName)`
  - `InboxMessageConfiguration(IdentityModuleConstants.SchemaName)`

### FshUser

File: `src/Modules/Identity/Modules.Identity/Features/v1/Users/FshUser.cs`

Extends `IdentityUser`:

- Profile fields:
  - `FirstName`, `LastName`, `ImageUrl`
  - `IsActive`
- Refresh tokens:
  - `string? RefreshToken` – hashed refresh token.
  - `DateTime RefreshTokenExpiryTime` – expiration timestamp.
- External identity:
  - `string? ObjectId`

### Db Initialization

File: `src/Modules/Identity/Modules.Identity/Data/IdentityDbInitializer.cs`

- Implements `IDbInitializer` from Persistence building block.
- Seeds:
  - Admin tenant and user.
  - Default roles and permissions.
- Called from Web host via:
  - `app.UseHeroMultiTenantDatabases();` which runs initializers discovered in DI.

### Eventing Services

In `IdentityModule.ConfigureServices` (`IdentityModule.cs`), Identity wires in the eventing building block:

- `services.AddEventingCore(builder.Configuration);`
- `services.AddEventingForDbContext<IdentityDbContext>();`
- `services.AddIntegrationEventHandlers(typeof(IdentityModule).Assembly);`

This enables:

- Access to `IOutboxStore` and `IInboxStore` for IdentityDbContext.
- Registration of integration event handlers (e.g., welcome email handler).

---

## Authentication & Tokens

### JwtOptions

File: `src/Modules/Identity/Modules.Identity/Authorization/Jwt/JwtOptions.cs`

Configurable via appsettings:

- `Issuer`
- `Audience`
- `SigningKey` (>= 32 characters)
- `AccessTokenMinutes` (default: 30)
- `RefreshTokenDays` (default: 7)

Validation ensures:

- Non-empty signing key, issuer, audience.
- Sufficient signing key length.

### TokenService (ITokenService)

Interface: `src/Modules/Identity/Modules.Identity.Contracts/Services/ITokenService.cs`  
Implementation: `src/Modules/Identity/Modules.Identity/Services/TokenService.cs`

Responsibilities:

- Issue JWT access and refresh tokens.

`IssueAsync`:

- Builds a symmetric signing key from `JwtOptions.SigningKey`.
- Creates an access token:
  - `JwtSecurityToken` with issuer, audience, claims, expiration `DateTime.UtcNow + AccessTokenMinutes`.
- Creates a refresh token:
  - `Guid`-based random token: `Convert.ToBase64String(Guid.NewGuid().ToByteArray())`.
  - Expiration `DateTime.UtcNow + RefreshTokenDays`.
- Logs issuance with `IdentityMetrics`.
- Returns `TokenResponse` DTO:

  ```csharp
  public sealed record TokenResponse(
      string AccessToken,
      string RefreshToken,
      DateTime RefreshTokenExpiresAt,
      DateTime AccessTokenExpiresAt);
  ```

> Note: The refresh token string is persisted hashed through `IdentityService` (see below).

### IdentityService (IIdentityService)

Interface: `src/Modules/Identity/Modules.Identity.Contracts/Services/IIdentityService.cs`  
Implementation: `src/Modules/Identity/Modules.Identity/Services/IdentityService.cs`

Responsibilities:

- Validate user credentials for login.
- Validate refresh tokens for token rotation.
- Persist refresh tokens in the user store.

#### ValidateCredentialsAsync

- Validates tenant context (must exist and be active; validity date not expired).
- Finds user by normalized email:
  - Checks password via `UserManager.CheckPasswordAsync`.
  - Ensures:
    - `user.IsActive == true`
    - `EmailConfirmed == true`
    - Tenant validity (`currentTenant.IsActive`, `ValidUpto`) is OK.
- Builds claims:
  - `Jti`, `NameIdentifier`, `Email`, `Name`, `MobilePhone`, `Fullname`, `Surname`, `Tenant`, `ImageUrl`.
  - Adds role claims from `UserManager.GetRolesAsync`.
- Returns `(user.Id, claims)` or throws `UnauthorizedException`.

#### ValidateRefreshTokenAsync

- Validates tenant context.
- Hashes provided refresh token using `HashToken(string token)`:

  - SHA-256 + Base64.

- Looks up `FshUser` by `RefreshToken == hashedToken`.
- Enforces:
  - `RefreshTokenExpiryTime > DateTime.UtcNow`.
  - `IsActive == true`.
  - `EmailConfirmed == true`.
  - Tenant active and valid (same checks as login).
- Rebuilds claims exactly like `ValidateCredentialsAsync`.
- Returns `(user.Id, claims)` or throws `UnauthorizedException`.

#### StoreRefreshTokenAsync

- Validates tenant context.
- Finds user by subject (Id).
- Hashes refresh token via `HashToken`.
- Updates:
  - `user.RefreshToken`
  - `user.RefreshTokenExpiryTime`
- Calls `UserManager.UpdateAsync(user)` and logs/throws `UnauthorizedException` on failure.

### Token Generation Endpoint

File: `src/Modules/Identity/Modules.Identity/Features/v1/Tokens/TokenGeneration/GenerateTokenEndpoint.cs`

Route:

- `POST /api/v{version:apiVersion}/identity/token`

Request:

- Body: `GenerateTokenCommand` (email, password).
- Header: `tenant` (defaults to `"root"` in docs; actual tenant resolution uses Finbuckle).

Handler: `GenerateTokenCommandHandler`:

- Validates credentials via `IIdentityService.ValidateCredentialsAsync`.
- On failure:
  - Audits login failure via `ISecurityAudit.LoginFailedAsync`.
  - Throws `UnauthorizedAccessException`.
- On success:
  - Audits login success via `ISecurityAudit.LoginSucceededAsync`.
  - Issues tokens via `ITokenService.IssueAsync`.
  - Persists refresh token via `IIdentityService.StoreRefreshTokenAsync`.
  - Audits token issuance via `ISecurityAudit.TokenIssuedAsync` with an SHA-256 fingerprint of the access token.
- Returns `TokenResponse` (access + refresh + expirations).

### Refresh Token Endpoint

Files:

- Endpoint: `src/Modules/Identity/Modules.Identity/Features/v1/Tokens/RefreshToken/RefreshTokenEndpoint.cs`
- Handler: `RefreshTokenCommandHandler.cs`
- DTOs:
  - `RefreshTokenCommand` & `RefreshTokenCommandResponse` in `Modules.Identity.Contracts`.

Route:

- `POST /api/v{version:apiVersion}/identity/token/refresh`

Request:

- Body: `RefreshTokenCommand`:
  - `string Token` – previously issued access token (may be expired).
  - `string RefreshToken` – the current refresh token.
- Header: `tenant`.

Handler flow:

1. Reads `ip`, `User-Agent`, `X-Client-Id` (default `"web"`).
2. Validates refresh token:
   - Calls `IIdentityService.ValidateRefreshTokenAsync(request.RefreshToken)`.
   - On invalid:
     - Audits `TokenRevokedAsync("unknown", clientId, "InvalidRefreshToken", ...)`.
     - Throws `UnauthorizedAccessException`.
3. Uses returned `(subject, claims)`.
4. Optionally parses the provided access token with `JwtSecurityTokenHandler.ReadJwtToken`:
   - Extracts `ClaimTypes.NameIdentifier`.
   - If present and mismatched with `subject`:
     - Audits `TokenRevokedAsync(subject, clientId, "RefreshTokenSubjectMismatch", ...)`.
     - Throws `UnauthorizedAccessException`.
5. Audits rotation of the previous token:
   - `TokenRevokedAsync(subject, clientId, "RefreshTokenRotated", ...)`.
6. Issues new tokens via `ITokenService.IssueAsync(subject, claims, ...)`.
7. Stores new refresh token via `IIdentityService.StoreRefreshTokenAsync`.
8. Audits new token issuance via `TokenIssuedAsync`.
9. Returns `RefreshTokenCommandResponse`:
   - `Token` – new access token.
   - `RefreshToken` – new refresh token.
   - `RefreshTokenExpiryTime` – expiration timestamp of new refresh token.

Validator:

- `RefreshTokenCommandValidator` ensures both `Token` and `RefreshToken` are non-empty.

---

## User & Role Features

User features are organized under `src/Modules/Identity/Modules.Identity/Features/v1/Users` with subfolders for each operation:

- `AssignUserRoles`
- `ChangePassword`
- `ConfirmEmail`
- `DeleteUser`
- `ForgotPassword`
- `GetUserById`
- `GetUserPermissions`
- `GetUserProfile`
- `GetUserRoles`
- `GetUsers`
- `RegisterUser`
- `ResetPassword`
- `SelfRegistration`
- `ToggleUserStatus`
- `UpdateUser`

Each feature follows this pattern:

- Contract: command/query + DTO in `Modules.Identity.Contracts.v1.Users.*`.
- Handler: uses `UserManager<FshUser>`, `RoleManager<FshRole>`, `IUserService`, or `IRoleService`.
- Endpoint: Minimal API extension method mapping to `IMediator`.
- Validator: FluentValidation class enforcing input rules where needed.

Examples:

- `GetUserByIdEndpoint`:
  - Route: `GET /users/{id:guid}`.
  - Returns `UserDto`.
  - Requires permission `IdentityPermissionConstants.Users.View`.
- `RegisterUserEndpoint`:
  - Route: `POST /users/register`.
  - Creates new user, sends confirmation email via `IMailService`.

---

## Security, Permissions & Authorization

Permissions:

- Defined in `IdentityPermissionConstants` (under `Features/v1/Users`).
- Permissions are applied via:
  - `.RequirePermission(IdentityPermissionConstants.Users.View)` extension on endpoints.
- Roles:
  - `IRoleService` exposes operations to assign permissions (role claims).

Authorization:

- JWT bearer authentication configured in Web building blocks.
- Policy-based authorization using claims and permissions.
- `PathAwareAuthorizationHandler` allows adjusting behavior based on route.

---

## Caching, Mailing, Storage Integration

Caching:

- The Identity module can use `ICacheService` (BuildingBlocks.Caching) for user-related caching (e.g., permissions, profile).

Mailing:

- `UserService` uses `IMailService` from BuildingBlocks.Mailing to:
  - Send email confirmation links.
  - Send password reset links.
  - Handle event-driven notifications (e.g., welcome emails) via integration event handlers.

Storage:

- Profile image upload uses `IStorageService` (typically `LocalStorageService`):
  - Saves file.
  - Stores URI in `FshUser.ImageUrl`.

---

## Auditing & Metrics

Auditing:

- Uses `ISecurityAudit` (`Modules.Auditing.Contracts`) for:
  - Login success/failure.
  - Token issuance.
  - Token revocation/rotation.
- Audit entries include:
  - UserId, UserName.
  - ClientId.
  - IP, UserAgent.
  - Token fingerprint (access token hash, never raw token).

Metrics:

- `IdentityMetrics` (singleton) tracks:
  - Token generation counts per user/email.
  - Potentially other Identity KPIs (logins, failures) – can be extended.

---

## Adding New Identity Endpoints

To add a feature (e.g., "Change email"):

1. **Contracts**:
   - Add a command/DTO under `Modules.Identity.Contracts.v1.Users.ChangeEmail`.
2. **Handler**:
   - Implement `ICommandHandler<ChangeEmailCommand, ChangeEmailResponse>` in `Modules.Identity` under `Features/v1/Users/ChangeEmail`.
3. **Validator**:
   - Implement `ChangeEmailValidator` using FluentValidation.
4. **Endpoint**:
   - Create a `ChangeEmailEndpoint` with `MapChangeEmailEndpoint` returning `RouteHandlerBuilder`.
   - Use `RequirePermission` to enforce appropriate permission constants.
5. **Module mapping**:
   - Update `IdentityModule.MapEndpoints` to call `group.MapChangeEmailEndpoint();`.

The rest (mediator wiring, validation, exception handling, auditing) is handled by the platform.

---

## Gaps & Possible Improvements

Some opportunities to enhance the Identity module further:

- **Refresh-token families**:
  - Current implementation supports one active refresh token per user.
  - For higher security, a token family approach could track chain of refreshes and detect reuse of older tokens.
- **Two-factor authentication**:
  - The infrastructure supports ASP.NET Identity 2FA providers but the module doesn’t yet expose endpoints/UI for it.
- **Lockout & brute-force protection**:
  - Login and refresh endpoints are rate-limited but could additionally leverage ASP.NET Identity lockout policies more aggressively.
- **More granular permissions**:
  - Split coarse-grained permissions (e.g., `Users.View`) into more granular operations if needed (view profile vs view roles vs view permissions).
- **Security headers & cookie options**:
  - Currently tokens are returned in JSON; for browser-based SPAs, an additional HttpOnly cookie-based flow could be provided as an option.

Even without these enhancements, the module provides a robust foundation for most enterprise identity scenarios in multi-tenant .NET 10 APIs.

---

## Eventing: User Registration Flow

Identity participates in the eventing building block for user registration.

### Integration Event Definition

Location: `src/Modules/Identity/Modules.Identity.Contracts/Events/UserRegisteredIntegrationEvent.cs`

Defined as:

```csharp
public sealed record UserRegisteredIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    string UserId,
    string Email,
    string FirstName,
    string LastName)
    : IIntegrationEvent;
```

### Publishing the Event via Outbox

In `UserService.RegisterAsync` (`src/Modules/Identity/Modules.Identity/Services/UserService.cs`):

- After successfully creating the user, assigning the basic role, and scheduling a confirmation email job:
  - The service constructs a `UserRegisteredIntegrationEvent` with:
    - `TenantId` from `IMultiTenantContextAccessor<AppTenantInfo>`.
    - `CorrelationId` (GUID for now; could later be tied to request correlation).
    - `Source = "Identity"`.
    - User details (`UserId`, `Email`, `FirstName`, `LastName`).
  - It injects `IOutboxStore` and calls:

    ```csharp
    await outboxStore.AddAsync(integrationEvent, cancellationToken);
    ```

- The event is persisted to the `OutboxMessages` table as part of the same transaction as the user creation.

### Consuming the Event: Welcome Email

Handler file: `src/Modules/Identity/Modules.Identity/Events/UserRegisteredEmailHandler.cs`

```csharp
public sealed class UserRegisteredEmailHandler
    : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly IMailService _mailService;

    public UserRegisteredEmailHandler(IMailService mailService)
        => _mailService = mailService;

    public async Task HandleAsync(UserRegisteredIntegrationEvent @event, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(@event.Email))
        {
            return;
        }

        var mail = new MailRequest(
            To: new Collection<string> { @event.Email },
            Subject: "Welcome!",
            Body: $"Hi {@event.FirstName}, thanks for registering.");

        await _mailService.SendAsync(mail, ct).ConfigureAwait(false);
    }
}
```

- Registered via:

  ```csharp
  services.AddIntegrationEventHandlers(typeof(IdentityModule).Assembly);
  ```

### Dispatching the Outbox

- `OutboxDispatcher` (from the Eventing building block) is registered for IdentityDbContext.
- A scheduler (such as a Hangfire recurring job) calls `OutboxDispatcher.DispatchAsync()`:
  - Reads pending `OutboxMessages`.
  - Deserializes payloads into integration events.
  - Publishes them via `IEventBus` (currently the in-memory implementation).
  - Marks messages as processed or dead-lettered after max retries.

### In-Memory Event Bus and Inbox

- `InMemoryEventBus`:
  - Resolves all `IIntegrationEventHandler<T>` from DI.
  - Invokes handlers for each published event.
- `IInboxStore` and `InboxMessage`:
  - Provide idempotency:
    - If an event has already been processed for a given handler, it is skipped.

This results in a clean, event-driven workflow where user registration triggers an integration event, which in turn drives a welcome email, all while respecting multi-tenancy and idempotent processing.
