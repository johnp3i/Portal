using System.Text.Json;

namespace Portal.Infrastructure.Models;

/// <summary>
/// Shared System.Text.Json options for the purchases dashboard (camelCase, DateOnly as yyyy-MM-dd).
/// </summary>
public static class PurchasesJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
