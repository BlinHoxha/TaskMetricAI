using System.Security.Cryptography;
using Npgsql;
using TaskMetricAI.Domain;
using TaskMetricAI.Infrastructure.Persistence;

/// <summary>Operator/CI-only role provisioning. Passwords are random, never returned or logged.</summary>
internal static class RuntimeRoleProvisioner
{
    public static async Task ProvisionAsync(string adminFile, string outputDirectory, bool ciBootstrap)
    {
        Require.That(ciBootstrap && Environment.GetEnvironmentVariable("CI") == "true", 403, "ci_only_operation");
        var admin = (await File.ReadAllTextAsync(adminFile)).Trim();
        var settings = new NpgsqlConnectionStringBuilder(admin);
        Require.That(settings.Host is "localhost" or "127.0.0.1" && settings.Database == "postgres", 422, "local_admin_connection_required");
        var output = Path.GetFullPath(outputDirectory);
        for (var directory = new DirectoryInfo(output); directory is not null; directory = directory.Parent)
        {
            Require.That(!directory.Exists || directory.LinkTarget is null, 422, "real_secret_directory_required");
            Require.That(!Directory.Exists(Path.Combine(directory.FullName, ".git")) && !File.Exists(Path.Combine(directory.FullName, ".git")),
                422, "outside_repository_secret_directory_required");
        }
        Directory.CreateDirectory(output);
        await using var connection = new NpgsqlConnection(admin); await connection.OpenAsync();
        Require.That(connection.PostgreSqlVersion.Major == 17, 422, "postgresql_17_required");
        if (ciBootstrap)
        {
            Require.That(Environment.GetEnvironmentVariable("CI") == "true", 403, "ci_only_operation");
            await CreateRole(connection, "taskmetric_setup", "postgres", output, createdb: true);
        }
        await CreateRole(connection, "taskmetric_runtime_dev", "taskmetricai_dev", output);
        await CreateRole(connection, "taskmetric_runtime_test", "taskmetricai_test", output);
        if (ciBootstrap)
        {
            var setup = await File.ReadAllTextAsync(Path.Combine(output, "taskmetric_setup.connection"));
            foreach (var database in new[] { "taskmetricai_dev", "taskmetricai_test" })
            {
                var target = new NpgsqlConnectionStringBuilder(setup) { Database = database };
                await LocalDatabaseOperator.CreateAndInitializeAsync(target.ConnectionString);
                await PostgreSqlHardening.GrantRuntimeAsync(target.ConnectionString);
                await WriteSecret(Path.Combine(output, database + ".connection"), target.ConnectionString);
            }
        }
    }

    private static async Task CreateRole(NpgsqlConnection connection, string role, string database, string output, bool createdb = false)
    {
        await using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=@role)", connection);
        exists.Parameters.AddWithValue("role", role);
        Require.That(await exists.ExecuteScalarAsync() is false, 409, "role_exists_no_password_rotation_permitted");
        var passfile = Path.Combine(output, role + ".pgpass");
        var reference = Path.Combine(output, role + ".connection");
        Require.That(!File.Exists(passfile) && !File.Exists(reference), 409, "secret_file_already_exists");
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var settings = new NpgsqlConnectionStringBuilder { Host = "localhost", Database = database, Username = role,
            Passfile = passfile, IncludeErrorDetail = false, NoResetOnClose = false };
        // The only identifiers are fixed literals above; the random hexadecimal password cannot contain SQL syntax.
        await using var create = new NpgsqlCommand($"CREATE ROLE \"{role}\" LOGIN NOSUPERUSER {(createdb ? "CREATEDB" : "NOCREATEDB")} NOCREATEROLE NOBYPASSRLS NOINHERIT NOREPLICATION PASSWORD '{password}'", connection);
        await create.ExecuteNonQueryAsync();
        await WriteSecret(passfile, $"localhost:5432:*:{role}:{password}\n");
        await WriteSecret(reference, settings.ConnectionString);
    }

    private static async Task WriteSecret(string path, string content)
    {
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        await using var writer = new StreamWriter(file); await writer.WriteAsync(content);
    }
}
