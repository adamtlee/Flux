using Microsoft.EntityFrameworkCore;
using Flux.Data;
using Flux.Services;
using Flux.Api.Startup;

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
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApplicationAuthorization();
builder.Services.AddDatabaseInitializer();

var app = builder.Build();
await app.InitializeDatabaseAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
