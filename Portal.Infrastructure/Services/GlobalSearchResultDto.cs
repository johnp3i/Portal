namespace Portal.Infrastructure.Services;

public class GlobalSearchResultDto
{
    public List<SearchResultGroup> Groups { get; set; } = new();
}

public class SearchResultGroup
{
    public string Type { get; set; } = null!;
    public string Label { get; set; } = null!;
    public List<SearchResultItem> Items { get; set; } = new();
}

public class SearchResultItem
{
    public int Id { get; set; }
    public string Primary { get; set; } = null!;
    public string Secondary { get; set; } = null!;
    public string Url { get; set; } = null!;
}
