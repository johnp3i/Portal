using Portal.Infrastructure.Models;

namespace Portal.Web.Models;

/// <summary>
/// View model for the _AttachmentPanel.cshtml partial view.
/// </summary>
public class AttachmentPanelViewModel
{
    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public List<AttachmentDto> Attachments { get; set; } = new();

    public int MaxAttachments { get; set; } = 5;

    public bool IsReadOnly { get; set; }
}
