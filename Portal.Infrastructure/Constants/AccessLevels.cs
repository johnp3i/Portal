namespace Portal.Infrastructure.Constants;

public static class AccessLevels
{
    public const string Full = "full";
    public const string ReadOnly = "readonly";
    public const string None = "none";

    public static readonly string[] All = { Full, ReadOnly, None };

    public static bool IsValid(string level) => All.Contains(level);

    /// <summary>
    /// Returns true if 'actual' meets or exceeds 'required'.
    /// Hierarchy: full > readonly > none
    /// </summary>
    public static bool MeetsRequirement(string actual, string required)
    {
        if (actual == Full) return true;
        if (actual == ReadOnly && required == ReadOnly) return true;
        return false;
    }
}
