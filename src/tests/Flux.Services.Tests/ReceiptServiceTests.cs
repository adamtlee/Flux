using Flux.Data;
using Flux.Data.Models;
using Flux.Services;
using Flux.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Flux.Services.Tests;

public sealed class ReceiptServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BankDbContext _context;
    private readonly ReceiptService _service;

    public ReceiptServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new BankDbContext(options);
        _context.Database.EnsureCreated();

        _service = new ReceiptService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateReceiptAsync_ValidRequest_PersistsReceiptAndItems()
    {
        var userId = Guid.NewGuid();
        var account = new BankAccount
        {
            OwnerUserId = userId,
            Owner = "member",
            AccountName = "Checking",
            Balance = 1200m,
            Type = AccountType.Checking
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        var model = new ReceiptUpsertModel(
            AccountId: account.Id,
            MerchantName: "Local Market",
            PurchasedAtUtc: DateTime.UtcNow,
            TotalAmount: 26m,
            CurrencyCode: "usd",
            Notes: "Groceries",
            Items:
            [
                new ReceiptItemUpsertModel("Milk", 2m, 3.5m),
                new ReceiptItemUpsertModel("Bread", 3m, 6.333333m)
            ]);

        var created = await _service.CreateReceiptAsync(userId, "member", model);

        Assert.True(created.Id > 0);
        Assert.Equal("USD", created.CurrencyCode);
        Assert.Equal(2, created.Items.Count);
        Assert.Equal(26m, created.TotalAmount);
    }

    [Fact]
    public async Task GetReceiptByIdAsync_NonOwnerAndNonAdmin_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var receipt = new Receipt
        {
            OwnerUserId = ownerId,
            OwnerUsername = "owner",
            MerchantName = "Shop",
            PurchasedAtUtc = DateTime.UtcNow,
            TotalAmount = 10m,
            CurrencyCode = "USD",
            Items =
            [
                new ReceiptItem { ProductName = "Item", Quantity = 1m, UnitPrice = 10m, LineTotal = 10m }
            ]
        };

        _context.Receipts.Add(receipt);
        await _context.SaveChangesAsync();

        var result = await _service.GetReceiptByIdAsync(receipt.Id, Guid.NewGuid(), isAdministrator: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReceiptAsync_TotalMismatch_ThrowsArgumentException()
    {
        var model = new ReceiptUpsertModel(
            AccountId: null,
            MerchantName: "Store",
            PurchasedAtUtc: DateTime.UtcNow,
            TotalAmount: 3m,
            CurrencyCode: "USD",
            Notes: null,
            Items:
            [
                new ReceiptItemUpsertModel("Product", 1m, 10m)
            ]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateReceiptAsync(Guid.NewGuid(), "member", model));
    }
}
