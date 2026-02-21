using Microsoft.EntityFrameworkCore;
using Flux.Data.Models;

namespace Flux.Data;

public class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }

    public DbSet<BankAccount> Accounts { get; set; }
}