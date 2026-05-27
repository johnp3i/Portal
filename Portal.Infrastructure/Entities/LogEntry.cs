namespace Portal.Infrastructure.Entities;

/// <summary>
/// A single Serilog log record from the [dbo].[Logs] table in Portal.Logging.
/// Read-only entity — no inserts, updates, or deletes from the application.
/// </summary>
public class LogEntry
{
    public long Id { get; set; }

    public string? Message { get; set; }

    public string? MessageTemplate { get; set; }

    public string? Level { get; set; }

    public DateTime TimeStamp { get; set; }

    public string? Exception { get; set; }

    public string? CorrelationId { get; set; }

    public string? UserId { get; set; }

    public int? BusinessId { get; set; }

    public string? SourceContext { get; set; }

    public string? RequestPath { get; set; }

    public string? MachineName { get; set; }
}
