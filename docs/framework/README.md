# FSH Framework Documentation Index

This folder contains framework-level documentation for the fullstackhero .NET 10 starter kit.

Use these documents as the primary reference for both human developers and AI agents when working in this repo.

## Overview

- `architecture.md`  
  High-level architecture of the framework: BuildingBlocks, Modules, Playground, cross-cutting concerns (auth, persistence, DDD, mediator, validation, exceptions, multitenancy, health, OpenTelemetry, rate limiting, versioning, etc.).

- `building-blocks.md`  
  Detailed description of the BuildingBlocks projects:
  - Core, Persistence, Caching, Mailing, Jobs, Storage, Web.
  - How they are meant to be used by modules and hosts.

- `module-identity.md`  
  Identity module deep dive:
  - Endpoints, token generation/refresh, ASP.NET Identity integration.
  - Persistence model (`FshUser`, `IdentityDbContext`), permissions, auditing, metrics.

- `module-auditing.md`  
  Auditing module deep dive:
  - `IAuditClient`, `ISecurityAudit`, audit envelopes and payloads.
  - Audit querying endpoints, persistence, and integration with exceptions and security events.

- `module-multitenancy.md`  
  Multitenancy module deep dive:
  - Tenant model, Finbuckle integration, migrations, health checks.
  - Tenant APIs (status, migrations, upgrade, activation).

- `using-framework-in-your-api.md`  
  How to consume the framework in any .NET 10 Web API:
  - Using `FSH.Playground.Api` as a reference.
  - Wiring modules, BuildingBlocks, configuration, and Aspire/AppHost.

- `developer-cookbook.md`  
  Practical recipes for developers and AI agents:
  - Add endpoints, modules, DbContexts, jobs.
  - Use specifications, mailing, storage, observability.
  - Guidance on patterns to follow and anti-patterns to avoid.

- `contribution-guidelines.md`  
  Contributor and coding guidelines:
  - Folder and project layout.
  - When to add modules vs. features.
  - Patterns to follow (Minimal APIs, Mediator, FluentValidation, DDD, specifications).
  - Security, multitenancy, and cross-cutting concerns expectations.
  - AI-agent specific do’s and don’ts.
