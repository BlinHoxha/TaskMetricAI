using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

[Trait("Category", "PostgreSQL")]
public sealed class ConcurrencyHardeningTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Assignment_and_archive_serialize_on_the_same_profile_row(bool archiveFirst)
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await Change(fixture, ProfileStatus.Validated, 0);
        await Change(fixture, ProfileStatus.Active, 1);
        await using var archive = await fixture.Session(fixture.A);
        await using var assign = await fixture.Session(fixture.A);
        if (archiveFirst)
        {
            await fixture.Service(archive).TransitionAsync(fixture.ProfileA, 1, ProfileStatus.Archived, 2, "retire");
            var waiting = fixture.Service(assign).AssignAsync(new(ScopeType.Organization, "org"), fixture.ProfileA, 1, "assign racing archive");
            await WaitForLock(fixture.Runtime, ((NpgsqlConnection)assign.Db.Database.GetDbConnection()).ProcessID);
            await archive.CommitAsync();
            var denied = await Assert.ThrowsAsync<OperationException>(() => waiting);
            Assert.Equal(409, denied.Status);
        }
        else
        {
            await fixture.Service(assign).AssignAsync(new(ScopeType.Organization, "org"), fixture.ProfileA, 1, "assign");
            // Archive's initial assignment query cannot see the uncommitted assignment. The DB trigger must remove it.
            var waiting = fixture.Service(archive).TransitionAsync(fixture.ProfileA, 1, ProfileStatus.Archived, 2, "retire racing assignment");
            await WaitForLock(fixture.Runtime, ((NpgsqlConnection)archive.Db.Database.GetDbConnection()).ProcessID);
            await assign.CommitAsync(); await waiting; await archive.CommitAsync();
        }
        await using var verify = await fixture.Session(fixture.A);
        Assert.Empty(await verify.Db.Set<ProfileAssignment>().ToArrayAsync());
        Assert.Equal(ProfileStatus.Archived, (await fixture.Service(verify).ReadProfileAsync(fixture.ProfileA, 1)).Status);
    }

    private static async Task Change(SecurityFixture fixture, ProfileStatus status, long revision)
    {
        await using var session = await fixture.Session(fixture.A);
        await fixture.Service(session).TransitionAsync(fixture.ProfileA, 1, status, revision, "test transition");
        await session.CommitAsync();
    }
    private static async Task WaitForLock(string connectionString, int pid)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var command = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE pid=@pid AND wait_event_type='Lock')", connection);
            command.Parameters.AddWithValue("pid", pid);
            if (await command.ExecuteScalarAsync() is true) return;
            await Task.Delay(25);
        }
        Assert.Fail("Concurrent operation did not reach the expected PostgreSQL row lock.");
    }
}
