namespace Flux.Api.Startup;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public interface IDatabaseSchemaInitializer
{
    Task InitializeSchemaAsync(CancellationToken cancellationToken = default);
}

public interface IDatabaseDataRepair
{
    Task RepairDataAsync(CancellationToken cancellationToken = default);
}
