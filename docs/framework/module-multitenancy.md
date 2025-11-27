# Multitenancy Module

The Multitenancy module provides tenant management and multi-tenant database orchestration for the framework.

Namespace root: `FSH.Modules.Multitenancy`  
Implementation: `src/Modules/Multitenancy/Modules.Multitenancy`  
Contracts: `src/Modules/Multitenancy/Modules.Multitenancy.Contracts`  
Web extras: `src/Modules/Multitenancy/Modules.Multitenancy.Web`

---

## Responsibilities

- Manage tenants (create, update, activate/deactivate).
- Store tenant configuration (connection strings, database provider, validity).
- Integrate with Finbuckle.MultiTenant for tenant resolution.
- Configure tenant-aware DbContexts and migrations.
- Provide endpoints to:
  - List tenants.
  - Get tenant status.
  - Upgrade tenants (run migrations).
  - Inspect tenant migrations.
- Expose health checks for tenant databases.

---

## Architecture

### MultitenancyModule

File: `src/Modules/Multitenancy/Modules.Multitenancy/MultitenancyModule.cs`

Implements `IModule`:

- **ConfigureServices**:
  - Registers:
    - Finbuckle.MultiTenant with tenant store and resolvers.
    - Multitenant-aware DbContexts using `AddHeroDbContext<TContext>()`.
    - Tenant services for CRUD, provisioning, and migrations.
    - `TenantMigrationsHealthCheck`.
  - Configures `MultitenancyOptions`:
    - `src/Modules/Multitenancy/Modules.Multitenancy/MultitenancyOptions.cs`
    - Contains settings for database provider, root tenant, etc.

- **MapEndpoints**:
  - Defines `api/v1/multitenancy` route group.
  - Maps endpoints for:
    - `GetTenants`
    - `CreateTenant`
    - `ChangeTenantActivation`
    - `GetTenantStatus`
    - `UpgradeTenant`
    - `GetTenantMigrations`
  - Endpoints use Mediator and are permission-protected (admin-only operations).

---

## Tenant Model & Persistence

### Tenant Entity

Located in `Data` folder (e.g., `Tenant` or `AppTenantInfo` type).

Key fields (typical pattern, check concrete type in `Data`):

- `Id` – tenant identifier (string).
- `Name` – human-friendly tenant name.
- `ConnectionString` – per-tenant DB connection.
- `DatabaseProvider` – e.g., `POSTGRESQL`.
- `IsActive` – whether tenant is allowed to login/use the system.
- `ValidUpto` – optional validity cutoff.

### Tenant DbContext

DbContext in `Data` handles tenant metadata storage in a shared catalog database.

Finbuckle:

- Multi-tenant contexts use the tenant info to:
  - Select connection string at runtime.
  - Apply per-tenant migrations.

---

## Tenant Health & Migrations

### TenantMigrationsHealthCheck

File: `src/Modules/Multitenancy/Modules.Multitenancy/TenantMigrationsHealthCheck.cs`

Responsibilities:

- For each tenant:
  - Verify whether migrations are up-to-date.
  - Check basic connectivity.
- Report aggregated health status:
  - Healthy if all tenants are OK.
  - Degraded/unhealthy if any tenant DB is out of sync.

This is wired up into the health check pipeline via Web building blocks.

### Upgrade Tenant

Endpoint: `src/Modules/Multitenancy/Modules.Multitenancy/Features/v1/UpgradeTenant/UpgradeTenantEndpoint.cs`

- Route example: `POST /api/v1/multitenancy/tenants/{tenantId}/upgrade`
- Handler: `UpgradeTenantCommandHandler`:
  - Runs migrations for the given tenant.
  - Uses `ITenantService` to orchestrate DB schema upgrade.
  - Returns status (success/failure).

---

## Contracts & Endpoints

Contracts in `Modules.Multitenancy.Contracts`:

- `CreateTenantCommand`
- `ChangeTenantActivationCommand`
- `GetTenantsQuery`
- `GetTenantStatusQuery`
- `GetTenantMigrationsQuery`
- `UpgradeTenantCommand`

Endpoints in `Modules.Multitenancy`:

- `Features/v1/GetTenants`
- `Features/v1/GetTenantStatus`
- `Features/v1/GetTenantMigrations`
- `Features/v1/UpgradeTenant`
- `Features/v1/CreateTenant`
- `Features/v1/ChangeTenantActivation`

Each endpoint:

- Is a Minimal API route mapping.
- Uses `IMediator` to dispatch command/query.
- Applies permission checks (e.g., tenant admin).

Example: `GetTenantStatusEndpoint.cs`

- Route: `GET /api/v1/multitenancy/tenants/{id}/status`
- Handler: `GetTenantStatusQueryHandler`:
  - Uses tenant store and health info to compute tenant status response.

---

## Multitenancy in Identity & Other Modules

The Identity module depends on multitenancy:

- `IdentityService` uses `IMultiTenantContextAccessor<AppTenantInfo>`:
  - Validates that `currentTenant.Id` is not null/empty.
  - Checks `IsActive` and `ValidUpto`.
  - Uses tenant ID in user claims (`ClaimConstants.Tenant`).
  - Disallows login/refresh if tenant is inactive or expired.

EF Core:

- IdentityDbContext and other module DbContexts:
  - Are configured with `.IsMultiTenant()` in entity configurations.
  - Include `TenantId` fields to specify record ownership.

Web:

- Middleware from BuildingBlocks.Web sets current tenant based on:
  - Hostname, header, or other Finbuckle resolvers as configured.

---

## Adding Multi-Tenant Aware Modules

To make a new module tenant-aware:

1. Use Finbuckle’s multi-tenant DbContext base classes.
2. In entity configurations:
   - Mark entities with `.IsMultiTenant()`.
3. Use `IMultiTenantContextAccessor<AppTenantInfo>` in services:
   - Validate tenant context at entry points.
   - Use `TenantId` when querying/saving.
4. Add health checks using `TenantMigrationsHealthCheck` where appropriate.

---

## Gaps & Potential Improvements

Potential enhancements for Multitenancy:

- **Tenant provisioning automation**:
  - Background job to automatically run migrations and seed data when a new tenant is created.
- **Per-tenant feature flags**:
  - Extend tenant model to include feature configuration.
- **Tenant-level observability**:
  - Enrich telemetry and audit events with tenant info in a more consistent way across all modules.
- **Cross-tenant admin tools**:
  - Additional endpoints (or UI) for operations to:
    - Broadcast messages to tenants.
    - Monitor per-tenant resource utilization.

The existing module already covers key needs: consistent tenant management, per-tenant DBs, and health/migration status management.

