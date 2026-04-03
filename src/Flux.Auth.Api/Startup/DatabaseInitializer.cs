namespace Flux.Auth.Api.Startup;

public static class DatabaseInitializerExtensions
{
    public static IServiceCollection AddDatabaseInitializer(this IServiceCollection services)
    {
        services.AddScoped<IDatabaseSchemaInitializer, DatabaseSchemaInitializer>();
        services.AddScoped<IDatabaseDataRepair, DatabaseDataRepair>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        await initializer.InitializeAsync(cancellationToken);
    }
}
