using System.Net.Mail;
using Portal.Infrastructure.Entities.Notification;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Resolves the recipient(s) for an owner-facing digest (Group 3). v1 sends to a single
/// PRIMARY recipient — the first valid override address if an override is configured, else the
/// business owner — with the owner optionally added as a CC on the same message. Tenant-less:
/// takes an explicit businessId and reads the Membership DB directly (background-service safe).
/// </summary>
public interface IDigestRecipientResolver
{
    Task<DigestRecipients?> ResolveAsync(int businessId, BusinessAssistantSetting? setting);
}

/// <summary>Resolved digest recipients: one primary "To", zero-or-more CC addresses.</summary>
public class DigestRecipients
{
    public string PrimaryEmail { get; set; } = null!;
    public List<string> CcEmails { get; set; } = new();
}

public class DigestRecipientResolver : IDigestRecipientResolver
{
    private static readonly char[] Delimiters = { ';', ',', '\n', '\r' };

    private readonly IOwnerEmailResolver _ownerEmailResolver;

    public DigestRecipientResolver(IOwnerEmailResolver ownerEmailResolver)
    {
        _ownerEmailResolver = ownerEmailResolver;
    }

    public async Task<DigestRecipients?> ResolveAsync(int businessId, BusinessAssistantSetting? setting)
    {
        try
        {
            var ownerEmail = await _ownerEmailResolver.ResolveAsync(businessId);
            var overrides = ParseValidEmails(setting?.RecipientOverride);

            // No override configured -> send to the owner only.
            if (overrides.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(ownerEmail))
                    return null; // fail-safe: nothing to send to.

                return new DigestRecipients { PrimaryEmail = ownerEmail! };
            }

            // Override configured -> primary is the first valid override address.
            var recipients = new DigestRecipients { PrimaryEmail = overrides[0] };

            // Any additional override addresses become CCs.
            for (var i = 1; i < overrides.Count; i++)
                recipients.CcEmails.Add(overrides[i]);

            // Optionally CC the owner (when set and not already the primary/CC).
            var includeOwner = setting?.IsRecipientOwnerIncluded ?? true;
            if (includeOwner
                && !string.IsNullOrWhiteSpace(ownerEmail)
                && !recipients.PrimaryEmail.Equals(ownerEmail, StringComparison.OrdinalIgnoreCase)
                && !recipients.CcEmails.Any(c => c.Equals(ownerEmail, StringComparison.OrdinalIgnoreCase)))
            {
                recipients.CcEmails.Add(ownerEmail!);
            }

            return recipients;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Splits a delimited list, trims, de-duplicates (case-insensitive), and keeps only
    /// syntactically valid emails. Mirrors NotificationAdminAlertService's recipient parsing.
    /// </summary>
    private static List<string> ParseValidEmails(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var part in raw.Split(Delimiters, StringSplitOptions.RemoveEmptyEntries))
        {
            var email = part.Trim();
            if (email.Length == 0 || !IsValidEmail(email) || !seen.Add(email))
                continue;
            result.Add(email);
        }

        return result;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
