using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Data;
using Flux.Data; 
using Flux.Data.Models;
using Flux.Services;
using Flux.Services.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<BankDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddScoped<IBankAccountService, BankAccountService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtKey) || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException("JWT settings are missing. Configure Jwt:Key, Jwt:Issuer, and Jwt:Audience in appsettings.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.FreeMember, policy =>
        policy.RequireRole(ApplicationRoles.Administrator, ApplicationRoles.PremiumMember, ApplicationRoles.FreeMember));

    options.AddPolicy(AuthorizationPolicies.PremiumMember, policy =>
        policy.RequireRole(ApplicationRoles.Administrator, ApplicationRoles.PremiumMember));

    options.AddPolicy(AuthorizationPolicies.Administrator, policy =>
        policy.RequireRole(ApplicationRoles.Administrator));
});

var app = builder.Build();

// Ensure data directory exists before database operations
var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDirectory);

// Ensure the database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BankDbContext>();
    context.Database.EnsureCreated();
    context.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS UserAccounts (
            Id TEXT NOT NULL PRIMARY KEY,
            Username TEXT NOT NULL,
            Role TEXT NOT NULL,
            PasswordHash TEXT NOT NULL,
            PasswordSalt TEXT NOT NULL,
            PasswordIterations INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL
        );
    ");

    context.Database.ExecuteSqlRaw(@"
        CREATE UNIQUE INDEX IF NOT EXISTS IX_UserAccounts_Username ON UserAccounts (Username);
    ");

    EnsureColumnExists(context, "UserAccounts", "Role", "TEXT NOT NULL DEFAULT 'FreeMember'");
    EnsureColumnExists(context, "Accounts", "OwnerUserId", "TEXT NULL");

    var firstUser = context.UserAccounts
        .OrderBy(user => user.CreatedAt)
        .FirstOrDefault();

    if (firstUser is not null)
    {
        if (string.IsNullOrWhiteSpace(firstUser.Role) || firstUser.Role != ApplicationRoles.Administrator)
        {
            firstUser.Role = ApplicationRoles.Administrator;
        }

        var usersWithoutRole = context.UserAccounts
            .Where(user => user.Id != firstUser.Id && string.IsNullOrWhiteSpace(user.Role));

        foreach (var user in usersWithoutRole)
        {
            user.Role = ApplicationRoles.FreeMember;
        }

        var orphanAccounts = context.Accounts
            .Where(account => account.OwnerUserId == Guid.Empty);

        foreach (var account in orphanAccounts)
        {
            account.OwnerUserId = firstUser.Id;
            if (string.IsNullOrWhiteSpace(account.Owner))
            {
                account.Owner = firstUser.Username;
            }
            account.UpdatedAt = DateTime.UtcNow;
        }

        context.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static void EnsureColumnExists(BankDbContext context, string tableName, string columnName, string columnDefinition)
{
    var connection = context.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
    {
        connection.Open();
    }

    using var checkCommand = connection.CreateCommand();
    checkCommand.CommandText = $"PRAGMA table_info({tableName});";

    var columnExists = false;
    using (var reader = checkCommand.ExecuteReader())
    {
        while (reader.Read())
        {
            var existingColumnName = reader.GetString(1);
            if (string.Equals(existingColumnName, columnName, StringComparison.OrdinalIgnoreCase))
            {
                columnExists = true;
                break;
            }
        }
    }

    if (!columnExists)
    {
        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        alterCommand.ExecuteNonQuery();
    }
}
