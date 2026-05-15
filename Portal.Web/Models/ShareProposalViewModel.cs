using System.ComponentModel.DataAnnotations;

namespace Portal.Web.Models;

/// <summary>
/// View model for the proposal share dialog form submission.
/// </summary>
public class ShareProposalViewModel
{
    [Required]
    public DateTimeOffset ExpiresAtUtc { get; set; }

    public List<int> HeroLogoIds { get; set; } = new();

    public int? MetaLogoId { get; set; }
}
