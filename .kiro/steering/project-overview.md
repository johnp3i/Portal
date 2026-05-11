---
inclusion: always
---

# Portal Project Overview

## Project Identity

- **Repository**: Portal
- **Organization**: 3nventors
- **Type**: ASP.NET Core MVC Web Application
- **Database Approach**: Database-First (EF Core scaffolding)

## Architecture

### Pattern: MVC + Service Layer

```
Controller → Service → Repository → Database
                ↕
         Message Bus (MassTransit/RabbitMQ)
                ↕
         Real-time (SignalR)
```

### Technology Stack

| Layer | Technology |
|-------|-----------|
| Web Framework | ASP.NET Core MVC |
| ORM | Entity Framework Core (Database-First) |
| Message Bus | MassTransit + RabbitMQ |
| Real-time | SignalR |
| Logging | Serilog |
| Authentication | ASP.NET Core Identity |

## Key Conventions

### Code Organization
- Controllers handle HTTP concerns only — delegate logic to services
- Services contain business logic and orchestration
- Repositories handle all data access (see repository-standards global steering)
- Models map to database entities via EF Core scaffolding

### Database
- SQL Server with `[dbo]` schema
- Naming follows the SQL schema design steering (global)
- Full table names in queries — no short aliases
- BIT columns prefixed with `Is` or `Has`
- Foreign keys follow `<TableName>Id` convention

### Error Handling
- Repositories: try/catch with rethrow (`throw;`)
- Controllers: catch, log via `SystemLoggerExtensions`, return appropriate response
- Never swallow exceptions silently

### Security
- Input validation on all endpoints
- Parameterized queries (SqlParameter) — never string concatenation
- ASP.NET Core Identity for auth
- Credentials in User Secrets, not appsettings.json
