# Implementation Plan: Serilog Structured Logging

## Overview

This plan implements structured logging persistence for the Portal platform by creating a dedicated `Portal.Logging` SQL Server database, installing the required NuGet packages, configuring the Serilog MSSqlServer sink with custom column mappings, and adding a `LoggingEnrichmentMiddleware` to push UserId and BusinessId into the LogContext per request. The implementation is incremental — each task builds on the previous — and ends with integration wiring and verification.

## Tasks

- [x] 1. Create migration script and database schema
  - [x] 1.1 Create the `Portal.Database/Migrations/Logging/001_CreateLoggingDatabase.sql` migration script
    - Create the `Portal.Database/Migrations/Logging/` subdirectory
    - Write an idempotent SQL script that creates the `Portal.Logging` database if it does not exist
    - Create the `[dbo].[Logs]` table with columns: Id (BIGINT IDENTITY PK), Message (NVARCHAR(MAX) NOT NULL), MessageTemplate (NVARCHAR(MAX) NULL), Level (NVARCHAR(128) NOT NULL), TimeStamp (DATETIME2 NOT NULL), Exception (NVARCHAR(MAX) NULL), CorrelationId (NVARCHAR(128) NULL), UserId (NVARCHAR(450) NULL), BusinessId (INT NULL), SourceContext (NVARCHAR(512) NULL), RequestPath (NVARCHAR(512) NULL), MachineName (NVARCHAR(128) NULL)
    - Create non-clustered indexes: IX_Logs_TimeStamp on TimeStamp, IX_Logs_Level on Level, IX_Logs_BusinessId on BusinessId
    - Ensure the script is idempotent (uses IF NOT EXISTS checks)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 2. Install NuGet packages and add connection string
  - [x] 2.1 Add NuGet package references to Portal.Web.csproj
    - Add `Serilog.Sinks.MSSqlServer` version 7.0.1
    - Add `Serilog.Enrichers.Environment` version 3.0.1
    - _Requirements: 2.1, 2.2_

  - [x] 2.2 Add the `LoggingDb` connection string to appsettings.json
    - Add a `LoggingDb` entry under `ConnectionStrings` pointing to `Portal.Logging` database
    - Use the same server instance as the existing Portal and Membership connection strings
    - _Requirements: 3.2, 1.1_

- [x] 3. Implement LoggingEnrichmentMiddleware
  - [x] 3.1 Create `Portal.Web/Middleware/LoggingEnrichmentMiddleware.cs`
    - Implement the middleware class that extracts UserId from `ClaimTypes.NameIdentifier` and BusinessId from the `BusinessId` claim
    - Push both values into the Serilog `LogContext` using `LogContext.PushProperty`
    - Handle unauthenticated requests by pushing null for both properties
    - Use `int.TryParse` for BusinessId to gracefully handle non-integer claim values
    - _Requirements: 4.3, 4.4, 4.5_

  - [x]* 3.2 Write property test: Enrichment middleware preserves claim values
    - **Property 1: Enrichment middleware preserves claim values in LogContext**
    - Generate random strings for UserId, random ints for BusinessId, verify middleware pushes exact values into LogContext
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 4.3, 4.4**

  - [x]* 3.3 Write property test: Unauthenticated requests produce null enrichment
    - **Property 2: Unauthenticated requests produce null enrichment values**
    - Generate HttpContexts with no user, anonymous user, or user with missing claims
    - Verify middleware pushes null for both UserId and BusinessId
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 4.5**

- [x] 4. Checkpoint - Verify middleware and packages
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Configure Serilog MSSqlServer sink in Program.cs
  - [x] 5.1 Add SelfLog configuration at application startup
    - Enable `Serilog.Debugging.SelfLog` to write to `logs/serilog-selflog-.txt` using append mode
    - Place before any Serilog configuration so configuration errors are captured
    - _Requirements: 6.3_

  - [x] 5.2 Extend the existing `UseSerilog` configuration with MSSqlServer sink
    - Add `.Enrich.WithMachineName()` to the enrichment pipeline
    - Add `.WriteTo.MSSqlServer(...)` with connection string from `LoggingDb`
    - Configure `MSSqlServerSinkOptions`: TableName = "Logs", SchemaName = "dbo", AutoCreateSqlTable = true (dev only), BatchPostingLimit = 50, BatchPeriod = 5 seconds
    - Implement `GetColumnOptions()` helper method: remove Properties XML column, add custom columns (CorrelationId, UserId, BusinessId, SourceContext, RequestPath, MachineName), set TimeStamp.ConvertToUtc = true
    - Preserve existing Console and File sinks unchanged
    - _Requirements: 3.1, 3.3, 3.4, 3.5, 3.6, 3.7, 4.1, 4.2, 5.1, 5.2, 5.3, 6.4_

  - [x] 5.3 Register LoggingEnrichmentMiddleware in the request pipeline
    - Add `app.UseMiddleware<LoggingEnrichmentMiddleware>()` after `UseAuthentication()` and `UseAuthorization()` but before `MapControllerRoute()`
    - _Requirements: 4.3, 4.4, 4.5, 5.4_

- [x] 6. Checkpoint - Verify full configuration
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Integration and smoke tests
  - [x]* 7.1 Write integration test: log entry reaches database with correct columns
    - Configure a test host with the MSSqlServer sink pointing to a test database
    - Write a log entry with enriched context, flush the sink, query the Logs table
    - Verify CorrelationId, UserId, BusinessId, SourceContext, MachineName columns are populated
    - _Requirements: 3.1, 3.4, 5.4_

  - [x]* 7.2 Write smoke test: application resilience when logging database is unreachable
    - Configure an invalid LoggingDb connection string
    - Verify the application starts and serves HTTP requests without error
    - Verify Console and File sinks continue writing
    - _Requirements: 6.1, 6.2_

  - [x]* 7.3 Write smoke test: migration script idempotency and schema correctness
    - Execute the migration script twice against a test database
    - Verify no errors on second execution
    - Verify all columns, types, and indexes exist after migration
    - _Requirements: 7.2, 7.3, 7.4_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The migration script lives in `Portal.Database/Migrations/Logging/` to keep logging migrations separate from Portal DB migrations
- NuGet package versions (Serilog.Sinks.MSSqlServer 7.0.1, Serilog.Enrichers.Environment 3.0.1) are pinned for compatibility with the existing Serilog.AspNetCore 8.0.3 stack
- The middleware must be registered after authentication/authorization so claims are available

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["2.2", "3.1"] },
    { "id": 2, "tasks": ["3.2", "3.3", "5.1"] },
    { "id": 3, "tasks": ["5.2"] },
    { "id": 4, "tasks": ["5.3"] },
    { "id": 5, "tasks": ["7.1", "7.2", "7.3"] }
  ]
}
```
