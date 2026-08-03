namespace Portal.Infrastructure.Models.ProductInsights;

public class ProductKpiDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalUnits { get; set; }
    public decimal AvgSellingPrice { get; set; }
    public decimal GrossMargin { get; set; }
    public decimal MarginPercentage { get; set; }
    public DateTime? LastSoldDate { get; set; }
    public int InvoiceCount { get; set; }
}

public class ProductCustomerDto
{
    public string CustomerName { get; set; } = null!;
    public decimal Units { get; set; }
    public decimal Revenue { get; set; }
    public DateTime LastPurchase { get; set; }
}

public class ProductCustomerSummaryDto
{
    public int UniqueCustomerCount { get; set; }
    public decimal RepeatPurchaseRate { get; set; }
}

public class ProductForecastDto
{
    public decimal AvgMonthlyUnits { get; set; }
    public decimal AvgMonthlyRevenue { get; set; }
    public decimal Forecast30Revenue { get; set; }
    public decimal Forecast60Revenue { get; set; }
    public decimal Forecast90Revenue { get; set; }
    public decimal Forecast30Units { get; set; }
    public decimal Forecast60Units { get; set; }
    public decimal Forecast90Units { get; set; }
}

public class ProductPipelineDto
{
    public int ActiveLeadCount { get; set; }
    public decimal EstimatedValue { get; set; }
    public decimal ConversionRate { get; set; }
    public List<PipelineLeadDto> Leads { get; set; } = new();
}

public class PipelineLeadDto
{
    public int LeadId { get; set; }
    public string Title { get; set; } = null!;
    public string Stage { get; set; } = null!;
    public decimal EstimatedValue { get; set; }
    public string? AssignedTo { get; set; }
}

public class ProductDetailViewModel
{
    public Portal.Infrastructure.Entities.Product Product { get; set; } = null!;
    public string? SupplierName { get; set; }
    public string? ProductTypeName { get; set; }
    public string CurrencySymbol { get; set; } = "€";

    public ProductKpiDto Kpis { get; set; } = new();
    public List<ProductCustomerDto> TopCustomers { get; set; } = new();
    public ProductCustomerSummaryDto CustomerSummary { get; set; } = new();
    public List<MonthlyProductRevenueDto> MonthlyTrend { get; set; } = new();
    public List<Portal.Infrastructure.Entities.ProductPriceHistory> PriceHistory { get; set; } = new();
    public ProductForecastDto? Forecast { get; set; }
    public ProductPipelineDto? Pipeline { get; set; }
    public bool IsProfessional { get; set; }
}

public class MonthlyProductRevenueDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = null!;
    public decimal Revenue { get; set; }
}
