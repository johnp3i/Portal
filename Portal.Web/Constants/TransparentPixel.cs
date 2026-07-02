namespace Portal.Web.Constants;

/// <summary>
/// Static 1x1 transparent PNG bytes used for the tracking pixel response.
/// Pre-computed to avoid repeated file I/O.
/// </summary>
public static class TransparentPixel
{
    /// <summary>
    /// Minimal valid 1x1 transparent PNG (67 bytes).
    /// </summary>
    public static readonly byte[] Bytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVQI12NgAAIABQAB" +
        "Nl7pcQAAAABJRU5ErkJggg==");
}
