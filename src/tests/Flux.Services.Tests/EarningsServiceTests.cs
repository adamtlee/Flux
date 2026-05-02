using Flux.Data;
using Flux.Data.Models;
using Flux.Services;
using Flux.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Flux.Services.Tests;

public sealed class EarningsServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BankDbContext _context;
    private readonly EarningsService _service;

    public EarningsServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new BankDbContext(options);
        _context.Database.EnsureCreated();

        _service = new EarningsService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateEarningAsync_ValidRequest_PersistsAndNormalizesFields()
    {
        var userId = Guid.NewGuid();
        var model = new EarningUpsertModel(
            Label: " Primary Job ",
            AnnualGrossSalary: 50000m,
            DeductionMode: EarningDeductionMode.Percentage,
            DeductionValue: 22.5m,
            CurrencyCode: "usd");

        var created = await _service.CreateEarningAsync(userId, "member", model);

        Assert.True(created.Id > 0);
        Assert.Equal("Primary Job", created.Label);
        Assert.Equal("USD", created.CurrencyCode);
        Assert.Equal(22.5m, created.DeductionValue);
        Assert.Equal(userId, created.OwnerUserId);
    }

    [Fact]
    public async Task GetEarningsAsync_NonAdmin_ReturnsOwnedEntriesOnly()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        _context.Earnings.AddRange(
            new Earning
            {
                OwnerUserId = ownerId,
                OwnerUsername = "owner",
                Label = "Primary Job",
                AnnualGrossSalary = 75000m,
                DeductionMode = EarningDeductionMode.Percentage,
                DeductionValue = 25m,
                CurrencyCode = "USD"
            },
            new Earning
            {
                OwnerUserId = otherId,
                OwnerUsername = "other",
                Label = "Side Contract",
                AnnualGrossSalary = 12000m,
                DeductionMode = EarningDeductionMode.Flat,
                DeductionValue = 1000m,
                CurrencyCode = "USD"
            });

        await _context.SaveChangesAsync();

        var results = await _service.GetEarningsAsync(ownerId, isAdministrator: false);

        Assert.Single(results);
        Assert.Equal(ownerId, results[0].OwnerUserId);
    }

    [Fact]
    public async Task UpdateEarningAsync_NonOwner_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var earning = new Earning
        {
            OwnerUserId = ownerId,
            OwnerUsername = "owner",
            Label = "Primary Job",
            AnnualGrossSalary = 82000m,
            DeductionMode = EarningDeductionMode.Percentage,
            DeductionValue = 24m,
            CurrencyCode = "USD"
        };

        _context.Earnings.Add(earning);
        await _context.SaveChangesAsync();

        var result = await _service.UpdateEarningAsync(
            earning.Id,
            otherId,
            isAdministrator: false,
            new EarningUpsertModel("Updated", 90000m, EarningDeductionMode.Flat, 5000m, "USD"));

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteEarningAsync_OwnedEntry_RemovesEntry()
    {
        var userId = Guid.NewGuid();
        var earning = new Earning
        {
            OwnerUserId = userId,
            OwnerUsername = "member",
            Label = "Consulting",
            AnnualGrossSalary = 18000m,
            DeductionMode = EarningDeductionMode.Flat,
            DeductionValue = 1200m,
            CurrencyCode = "USD"
        };

        _context.Earnings.Add(earning);
        await _context.SaveChangesAsync();

        var deleted = await _service.DeleteEarningAsync(earning.Id, userId, isAdministrator: false);

        Assert.True(deleted);
        Assert.Empty(_context.Earnings);
    }

    [Fact]
    public async Task GetSummaryAsync_MultipleEntries_ReturnsCombinedGrossAndNetBreakdowns()
    {
        var userId = Guid.NewGuid();
        _context.Earnings.AddRange(
            new Earning
            {
                OwnerUserId = userId,
                OwnerUsername = "member",
                Label = "Primary Job",
                AnnualGrossSalary = 50000m,
                DeductionMode = EarningDeductionMode.Percentage,
                DeductionValue = 20m,
                CurrencyCode = "USD"
            },
            new Earning
            {
                OwnerUserId = userId,
                OwnerUsername = "member",
                Label = "Seasonal Job",
                AnnualGrossSalary = 10000m,
                DeductionMode = EarningDeductionMode.Flat,
                DeductionValue = 12000m,
                CurrencyCode = "USD"
            });

        await _context.SaveChangesAsync();

        var summary = await _service.GetSummaryAsync(userId, isAdministrator: false);

        Assert.Equal(2, summary.Entries.Count);
        Assert.Equal(60000m, summary.TotalGross.Annual);
        Assert.Equal(40000m, summary.TotalNet.Annual);
        Assert.Equal(20000m, summary.TotalAnnualDeductions);
        Assert.Equal(3333.33m, summary.TotalNet.Monthly);
        Assert.Contains(summary.Entries, item => item.Label == "Seasonal Job" && item.AnnualNetSalary == 0m);
    }
}