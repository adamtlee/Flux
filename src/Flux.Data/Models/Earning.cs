namespace Flux.Data.Models;

public sealed class Earning
{
    public int Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public string OwnerUsername { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public decimal AnnualGrossSalary { get; set; }

    public EarningDeductionMode DeductionMode { get; set; }

    public decimal DeductionValue { get; set; }

    public string CurrencyCode { get; set; } = "USD";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum EarningDeductionMode
{
    Percentage,
    Flat
}