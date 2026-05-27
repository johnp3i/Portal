# Requirements Document

## Introduction

This feature establishes a dedicated `Portal.Logging` SQL Server database for storing structured application logs, and extends the existing Serilog configuration to write structured log entries to this database via the MSSqlServer sink. The existing Console and File sinks remain operational. The logging database provides a queryable, persistent store for application diagnostics, error tracking, and operational visibility — separate from the Portal and Membership databases to avoid polluting transactional data with high-volume log entries.

## Glossary

- **Portal_Web**: The ASP.NET Core MVC 8 web application that serves the Portal platform.
- **Logging_Database**: A dedicated SQL Server database (`Portal.Logging`) used exclusively for storing structured log entries written by Serilog.
- **Serilog_MSSqlServer_Sink**: The Serilog sink package (`Serilog.Sinks.MSSqlServer`) that writes structured log events to a SQL Server table.
- **Log_Entry**: A single structured log event containing timestamp, level, message, exception details, and contextual properties.
- **Log_Table**: The `[dbo].[Logs]` table in the Logging_Database that stores all structured log entries.
- **Log_Level**: The severity classification of a log entry (Verbose, Debug, Information, Warning, Error, Fatal).
- **Enricher**: A Serilog component that adds contextual properties (CorrelationId, UserId, BusinessId) to every log entry.
- **Structured_Property**: A key-value pair attached to a log entry that enables filtering and querying (e.g., UserId, BusinessId, SourceContext).
- **Self_Log**: Serilog's internal diagnostic output used to surface sink failures without crashing the application.

## Requirements

### Requirement 1: Logging Database Creation

**User Story:** As a platform operator, I want a dedicated SQL Server database for structured logs, so that log data is isolated from transactional business data and can grow independently without impacting Portal or Membership database performance.

#### Acceptance Criteria

1. THE Logging_Database SHALL be created as a SQL Server database named `Portal.Logging` on the same server instance as the Portal and Membership databases.
2. THE Logging_Database SHALL contain a `[dbo].[Logs]` table with columns: Id (BIGINT IDENTITY PK), Message (NVARCHAR(MAX)), MessageTemplate (NVARCHAR(MAX)), Level (NVARCHAR(128)), TimeStamp (DATETIME2 NOT NULL), Exception (NVARCHAR(MAX) NULL), Properties (NVARCHAR(MAX) NULL).
3. THE Log_Table SHALL include additional columns for structured properties: CorrelationId (NVARCHAR(128) NULL), UserId (NVARCHAR(450) NULL), BusinessId (INT NULL), SourceContext (NVARCHAR(512) NULL), RequestPath (NVARCHAR(512) NULL), MachineName (NVARCHAR(128) NULL).
4. THE Log_Table SHALL use BIGINT for the Id column to accommodate high-volume log entries over the application lifetime.
5. THE Log_Table SHALL include a non-clustered index on TimeStamp for efficient time-range queries.
6. THE Log_Table SHALL include a non-clustered index on Level for filtering by severity.
7. THE Log_Table SHALL include a non-clustered index on BusinessId for tenant-scoped log queries.

### Requirement 2: Serilog MSSqlServer Sink Installation

**User Story:** As a developer, I want the Serilog SQL Server sink package installed, so that the application can write structured logs directly to the Logging_Database.

#### Acceptance Criteria

1. THE Portal_Web SHALL reference the `Serilog.Sinks.MSSqlServer` NuGet package at a stable version compatible with Serilog.AspNetCore 8.0.3.
2. THE Portal_Web SHALL reference the `Serilog.Enrichers.Environment` NuGet package to provide MachineName enrichment.

### Requirement 3: Serilog Sink Configuration

**User Story:** As a developer, I want Serilog configured to write structured logs to the Logging_Database, so that all application events are persisted in a queryable SQL format alongside the existing Console and File sinks.

#### Acceptance Criteria

1. THE Portal_Web SHALL configure a MSSqlServer sink that writes to the `[dbo].[Logs]` table in the Logging_Database.
2. THE Portal_Web SHALL register a connection string named `LoggingDb` in the application configuration pointing to the `Portal.Logging` database.
3. THE Portal_Web SHALL configure the MSSqlServer sink to auto-create the Logs table if it does not exist on application startup.
4. THE Portal_Web SHALL configure the MSSqlServer sink to store structured properties (CorrelationId, UserId, BusinessId, SourceContext, RequestPath, MachineName) as dedicated columns rather than only in the Properties XML/JSON blob.
5. THE Portal_Web SHALL retain the existing Console sink for development output.
6. THE Portal_Web SHALL retain the existing File sink with daily rolling for all environments.
7. THE Portal_Web SHALL configure the MSSqlServer sink minimum level to Information for production and Debug for development environments.

### Requirement 4: Log Enrichment

**User Story:** As a developer, I want every log entry enriched with contextual information, so that logs can be filtered by user, tenant, request, and machine for effective diagnostics.

#### Acceptance Criteria

1. THE Portal_Web SHALL enrich all log entries with CorrelationId using the existing `Serilog.Enrichers.CorrelationId` package.
2. THE Portal_Web SHALL enrich all log entries with MachineName using the `Serilog.Enrichers.Environment` package.
3. THE Portal_Web SHALL enrich all log entries with UserId extracted from the authenticated user's claims on each HTTP request.
4. THE Portal_Web SHALL enrich all log entries with BusinessId extracted from the authenticated user's claims on each HTTP request.
5. WHEN a request is unauthenticated, THE Portal_Web SHALL write NULL for UserId and BusinessId properties in the log entry.

### Requirement 5: Existing Logging Integration

**User Story:** As a developer, I want all existing `ILogger<T>` usage throughout the codebase to flow through Serilog to the database sink automatically, so that no code changes are required in controllers, services, or repositories to benefit from structured database logging.

#### Acceptance Criteria

1. THE Portal_Web SHALL route all Microsoft.Extensions.Logging `ILogger<T>` calls through Serilog as the logging provider.
2. THE Portal_Web SHALL preserve the existing minimum level overrides: Microsoft namespace at Warning, Microsoft.Hosting.Lifetime at Information, Microsoft.EntityFrameworkCore at Warning.
3. THE Portal_Web SHALL continue to use `UseSerilogRequestLogging()` middleware to log HTTP request summaries.
4. WHEN a controller, service, or repository logs via `ILogger<T>`, THE Serilog_MSSqlServer_Sink SHALL write the entry to the Logging_Database without requiring any code changes at the call site.

### Requirement 6: Resilience and Failure Handling

**User Story:** As a platform operator, I want the logging infrastructure to handle database connectivity failures gracefully, so that a logging database outage does not crash the application or lose all diagnostic output.

#### Acceptance Criteria

1. IF the Logging_Database is unreachable, THEN THE Portal_Web SHALL continue operating without interruption to user-facing functionality.
2. IF the Logging_Database is unreachable, THEN THE Portal_Web SHALL continue writing logs to the Console and File sinks.
3. THE Portal_Web SHALL configure Serilog SelfLog to write internal sink errors to a dedicated file (`logs/serilog-selflog-.txt`) so that sink failures are diagnosable.
4. THE Portal_Web SHALL configure the MSSqlServer sink with batch posting enabled to reduce database round-trips and improve throughput.

### Requirement 7: Database Migration Script

**User Story:** As a developer, I want a migration script for the Logging_Database, so that the database and table can be created consistently across development, staging, and production environments.

#### Acceptance Criteria

1. THE Portal_Web project SHALL include a SQL migration script that creates the `Portal.Logging` database if it does not exist.
2. THE migration script SHALL create the `[dbo].[Logs]` table with all columns specified in Requirement 1.
3. THE migration script SHALL create the non-clustered indexes specified in Requirement 1.
4. THE migration script SHALL be idempotent — safe to run multiple times without error or data loss.
5. THE migration script SHALL follow the existing Portal.Database migration naming convention (sequential numbering with descriptive name).
