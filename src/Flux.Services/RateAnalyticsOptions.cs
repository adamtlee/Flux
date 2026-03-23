namespace Flux.Services;

public sealed class RateAnalyticsOptions
{
    public const string SectionName = "RateAnalytics";

    public CreditCardDefaults CreditCard { get; set; } = new();
    public SavingsDefaults Savings { get; set; } = new();

    public sealed class CreditCardDefaults
    {
        public decimal AprPercent { get; set; } = 24.99m;
        public decimal MinimumPaymentPercent { get; set; } = 2m;
        public decimal MinimumPaymentFlatAmount { get; set; } = 25m;
    }

    public sealed class SavingsDefaults
    {
        public decimal ApyPercent { get; set; } = 4.5m;
    }
}