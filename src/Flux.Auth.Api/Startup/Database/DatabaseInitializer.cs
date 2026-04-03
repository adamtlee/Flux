namespace Flux.Auth.Api.Startup;

public sealed class DatabaseInitializer(
    IDatabaseSchemaInitializer schemaInitializer,
    IDatabaseDataRepair dataRepair) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await schemaInitializer.InitializeSchemaAsync(cancellationToken);
        await dataRepair.RepairDataAsync(cancellationToken);
    }
}
