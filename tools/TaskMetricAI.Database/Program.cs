using TaskMetricAI.Domain;
using TaskMetricAI.Infrastructure.Persistence;
using Npgsql;

// Connection file lives outside Git and may reference a PostgreSQL passfile. Never print it.
if (args.Length == 3 && args[0] == "provision-ci")
{
    try
    {
        await RuntimeRoleProvisioner.ProvisionAsync(args[1], args[2], args[0] == "provision-ci");
        Console.WriteLine("Isolated runtime role credentials were written outside Git; no passwords are displayed."); return 0;
    }
    catch (OperationException ex) { Console.Error.WriteLine(ex.Code); return 1; }
    catch (Exception) { Console.Error.WriteLine("Role provisioning failed; inspect operator privileges and local configuration. No credentials are logged."); return 1; }
}
if ((args.Length != 2 && !(args.Length == 3 && args[0] == "grant-runtime")) || args[0] is not ("initialize" or "grant-runtime"))
{
    Console.Error.WriteLine("Usage: TaskMetricAI.Database initialize|grant-runtime <outside-Git-setup-connection-file> [approved-v3-scope-manifest.json (grant-runtime only)]");
    return 2;
}
try
{
    var connection = (await File.ReadAllTextAsync(args[1])).Trim();
    if (args[0] == "initialize") await LocalDatabaseOperator.CreateAndInitializeAsync(connection);
    else
    {
        var registrations = args.Length == 3
            ? System.Text.Json.JsonSerializer.Deserialize<MigrationScopeRegistration[]>(await File.ReadAllTextAsync(args[2]), TaskMetricAI.Application.Documents.Json)
                ?? throw new OperationException(422, "migration_scope_manifest_invalid")
            : [];
        await PostgreSqlHardening.GrantRuntimeAsync(connection, registrations);
    }
    Console.WriteLine("Isolated local TaskMetric database schema is ready.");
    return 0;
}
catch (OperationException ex) { Console.Error.WriteLine(ex.Code); return 1; }
catch (PostgresException ex) { Console.Error.WriteLine($"Database initialization failed (PostgreSQL {ex.SqlState}). No credentials are logged."); return 1; }
catch (Exception ex) { Console.Error.WriteLine($"Database initialization failed ({ex.GetType().Name}). Check local role, passfile and permissions; no credentials are logged."); return 1; }
