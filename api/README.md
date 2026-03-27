# Ayora API Solution

This folder contains the Visual Studio solution `Ayora.sln` and all .NET projects.

## Projects

- `Ayora.Api`: ASP.NET Core Web API (controllers, middleware, config)
- `Ayora.Application`: DTOs + interfaces + services + options
- `Ayora.Domain`: entities + repository contracts (no dependencies)
- `Ayora.Infrastructure`: Dapper repositories + SQL Server access + security implementations
- `Ayora.Shared`: shared utilities/contracts (API responses, errors, modularity, clock)

## Clean Architecture dependency direction

- Domain → (nothing)
- Application → Domain
- Infrastructure → Domain + Application
- Api → Application + Infrastructure + Shared
- Shared → shared cross-cutting primitives

## Modularity

- Future modules should live under `api/Modules/` as separate projects.
- Modules implement `Ayora.Shared.Modularity.IModule` and are discovered/mapped at runtime by the API host.

## Folder conventions (enforced)

- `Ayora.Api`: `Controllers/`, `Middleware/`, `Swagger/`, `Modularity/`
- `Ayora.Application`: `DTOs/`, `Interfaces/`, `Services/`, `Options/`
- `Ayora.Infrastructure`: `Repositories/`, `Security/`, `Data/`
- `Ayora.Domain`: `Entities/` (or `Models/`), `Interfaces/`, `Enums/`

## Keep this README up to date

If you add/remove projects, modules, or change layering rules/contracts, update this file.

