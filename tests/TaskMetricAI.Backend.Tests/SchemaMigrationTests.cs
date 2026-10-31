using Npgsql;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

[Collection("Database prerequisites")]
[Trait("Category", "PostgreSQL")]
public sealed class SchemaMigrationTests
{
    [Fact]
    public async Task Populated_v2_migration_requires_explicit_scopes_preserves_history_and_is_repeatable()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        // Include real immutable documents, catalog links, profiles, grants, mappings and audit history.
        await using (var seed = await fixture.Session(fixture.A))
        {
            var service = fixture.Service(seed);
            var metric = await service.AddMetricVersionAsync("migration-metric", new("Custom", "Retain content", new("1", "2", "3", "4", "5"), "migration fixture"), 0);
            await service.AddCatalogVersionAsync("migration-catalog", [metric.Reference], 0, "catalog fixture");
            await service.TransitionAsync(fixture.ProfileA, 1, ProfileStatus.Validated, 0, "validate");
            await service.TransitionAsync(fixture.ProfileA, 1, ProfileStatus.Active, 1, "activate");
            await service.AssignAsync(new(ScopeType.Organization, "org"), fixture.ProfileA, 1, "assignment fixture");
            await seed.CommitAsync();
        }
        var schema = "migration_test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(fixture.Setup); await connection.OpenAsync();
        await using (var tx = await connection.BeginTransactionAsync())
        {
            async Task Execute(string sql)
            { await using var command = new NpgsqlCommand(sql, connection, tx); await command.ExecuteNonQueryAsync(); }
            await Execute($"CREATE SCHEMA \"{schema}\"; SET LOCAL search_path=\"{schema}\",pg_catalog");
            var tables = new List<string>();
            await using (var query = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='public' AND tablename NOT IN ('ProjectRegistration','TeamRegistration') ORDER BY tablename", connection, tx))
            await using (var reader = await query.ExecuteReaderAsync())
                while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
            foreach (var table in tables)
                await Execute($"CREATE TABLE \"{schema}\".\"{table}\" (LIKE public.\"{table}\" INCLUDING ALL)");
            foreach (var tenant in new[] { fixture.A, fixture.B })
            {
                await using (var bind = new NpgsqlCommand("SELECT set_config('taskmetric.tenant',@tenant,true)", connection, tx))
                { bind.Parameters.AddWithValue("tenant", tenant); await bind.ExecuteNonQueryAsync(); }
                foreach (var table in tables.Where(x => x != "TaskMetricSchema"))
                {
                    await using var copy = new NpgsqlCommand($"INSERT INTO \"{schema}\".\"{table}\" SELECT * FROM public.\"{table}\" WHERE \"TenantId\"=@tenant", connection, tx);
                    copy.Parameters.AddWithValue("tenant", tenant); await copy.ExecuteNonQueryAsync();
                }
            }
            await Execute($"INSERT INTO \"{schema}\".\"TaskMetricSchema\" VALUES (2)");
            // Reproduce v2 RLS, functions, immutable triggers and original foreign keys, excluding only v3 registration FKs.
            var definitions = new List<string>();
            await using (var query = new NpgsqlCommand("SELECT pg_get_functiondef(oid) FROM pg_proc WHERE pronamespace='public'::regnamespace AND proname LIKE 'taskmetric_%'", connection, tx))
            await using (var reader = await query.ExecuteReaderAsync())
                while (await reader.ReadAsync()) definitions.Add(reader.GetString(0));
            foreach (var definition in definitions) await Execute(definition.Replace("public.", $"\"{schema}\".", StringComparison.Ordinal));
            foreach (var table in tables.Where(x => x is not ("TaskMetricSchema" or "TenantInstallation" or "TenantRegistration")))
                await Execute($"""
                    ALTER TABLE "{schema}"."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE "{schema}"."{table}" FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON "{schema}"."{table}" USING ("TenantId"=current_setting('taskmetric.tenant',true)) WITH CHECK ("TenantId"=current_setting('taskmetric.tenant',true));
                    """);
            var constraints = new List<(string Table, string Name, string Definition)>();
            await using (var query = new NpgsqlCommand("""
                SELECT c.relname,k.conname,pg_get_constraintdef(k.oid) FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid
                WHERE k.connamespace='public'::regnamespace AND k.contype='f' AND k.conname NOT LIKE 'FK_ProjectTeam_%'
                """, connection, tx))
            await using (var reader = await query.ExecuteReaderAsync())
                while (await reader.ReadAsync()) constraints.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            foreach (var constraint in constraints)
                await Execute($"ALTER TABLE \"{schema}\".\"{constraint.Table}\" ADD CONSTRAINT \"{constraint.Name}\" {constraint.Definition.Replace("public.", $"\"{schema}\".", StringComparison.Ordinal)}");
            var triggers = new List<string>();
            await using (var query = new NpgsqlCommand("SELECT pg_get_triggerdef(t.oid) FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid WHERE c.relnamespace='public'::regnamespace AND NOT t.tgisinternal", connection, tx))
            await using (var reader = await query.ExecuteReaderAsync())
                while (await reader.ReadAsync()) triggers.Add(reader.GetString(0));
            foreach (var trigger in triggers) await Execute(trigger.Replace("public.", $"\"{schema}\".", StringComparison.Ordinal));

            async Task<string> Snapshot()
            {
                var rows = new List<string>();
                foreach (var table in tables.Where(x => x != "TaskMetricSchema"))
                    foreach (var tenant in new[] { fixture.A, fixture.B })
                    {
                        await using var read = new NpgsqlCommand($"SELECT set_config('taskmetric.tenant',@tenant,true)", connection, tx);
                        read.Parameters.AddWithValue("tenant", tenant); await read.ExecuteNonQueryAsync();
                        read.CommandText = $"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text)::text,'[]') FROM \"{schema}\".\"{table}\" t";
                        rows.Add(table + ":" + tenant + ":" + (string)(await read.ExecuteScalarAsync())!);
                    }
                return string.Join('\n', rows);
            }
            var before = await Snapshot();
            await tx.SaveAsync("before_migration");
            var rejected = await Assert.ThrowsAsync<OperationException>(() => PostgreSqlHardening.ApplyWithinTransactionAsync(connection, tx, [], schema));
            Assert.Equal("migration_scope_manifest_required", rejected.Code);
            await tx.RollbackAsync("before_migration");
            await using (var unchanged = new NpgsqlCommand($"SELECT \"Version\"=2 AND to_regclass('\"{schema}\".\"ProjectRegistration\"') IS NULL FROM \"{schema}\".\"TaskMetricSchema\"", connection, tx))
                Assert.Equal(true, await unchanged.ExecuteScalarAsync());
            Assert.Equal(before, await Snapshot());
            var approved = new[] { fixture.A, fixture.B }.SelectMany(tenant => new[]
            { new MigrationScopeRegistration(tenant, ScopeType.JiraProject, "project"), new(tenant, ScopeType.Team, "team") }).ToArray();
            await PostgreSqlHardening.ApplyWithinTransactionAsync(connection, tx, approved, schema);
            Assert.Equal(before, await Snapshot());
            await PostgreSqlHardening.ApplyWithinTransactionAsync(connection, tx, approved, schema);
            Assert.Equal(before, await Snapshot());
            await using (var version = new NpgsqlCommand($"SELECT \"Version\" FROM \"{schema}\".\"TaskMetricSchema\"", connection, tx))
                Assert.Equal(3, await version.ExecuteScalarAsync());
            await using (var keys = new NpgsqlCommand("SELECT count(*) FROM pg_constraint WHERE connamespace=@schema::regnamespace AND conname LIKE 'FK_ProjectTeam_%' AND convalidated", connection, tx))
            { keys.Parameters.AddWithValue("schema", schema); Assert.Equal(2L, await keys.ExecuteScalarAsync()); }
            await tx.RollbackAsync();
        }
        await using var absent = new NpgsqlCommand("SELECT to_regnamespace(@schema) IS NULL", connection);
        absent.Parameters.AddWithValue("schema", schema); Assert.Equal(true, await absent.ExecuteScalarAsync());
    }
}
