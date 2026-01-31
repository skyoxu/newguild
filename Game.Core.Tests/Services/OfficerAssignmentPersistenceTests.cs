using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Domain;
using Game.Core.Repositories;
using Xunit;

namespace Game.Core.Tests.Services
{
    public class OfficerAssignmentPersistenceTests
    {
        private sealed class RepositoryFixture : IDisposable
        {
            private readonly FileSQLiteDatabase _db;
            public SQLiteGuildRepository Repository { get; }

            public RepositoryFixture(string dbPath)
            {
                _db = new FileSQLiteDatabase(dbPath);
                Repository = new SQLiteGuildRepository(_db);
            }

            public void Dispose()
            {
                _db.Dispose();
            }
        }

        private static Guild CreateGuildWithMembers(string guildId)
        {
            var guild = new Guild(guildId, "Alpha", DateTimeOffset.UtcNow);
            guild.AddMember(new GuildMember("m1", "Alice", GuildRole.Member));
            guild.AddMember(new GuildMember("m2", "Bob", GuildRole.Member));
            return guild;
        }

        // ACC:T38.2
        [Fact]
        public async Task Should_Persist_Officer_Assignments_Across_Save_And_Load()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"guild-officers-{Guid.NewGuid():N}.db");
            using var fixture = new RepositoryFixture(dbPath);
            var repository = fixture.Repository;

            var guild = CreateGuildWithMembers("g1");
            guild.AssignOfficer(OfficerSlot.Treasurer, "m1");

            await repository.SaveAsync(guild, CancellationToken.None);

            var rehydrated = await repository.GetByIdAsync("g1", CancellationToken.None);

            rehydrated.Should().NotBeNull();
            rehydrated!.GetOfficerAssignment(OfficerSlot.Treasurer).Should().Be("m1");
        }

        // ACC:T38.3
        [Fact]
        public async Task Should_Refuse_Officer_Assignment_When_Slot_Occupied_And_Keep_Persisted_State()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"guild-officers-{Guid.NewGuid():N}.db");
            using var fixture = new RepositoryFixture(dbPath);
            var repository = fixture.Repository;

            var guild = CreateGuildWithMembers("g1");
            guild.AssignOfficer(OfficerSlot.Commander, "m1");

            await repository.SaveAsync(guild, CancellationToken.None);

            var rehydrated = await repository.GetByIdAsync("g1", CancellationToken.None);

            rehydrated.Should().NotBeNull();
            var assigned = rehydrated!.AssignOfficer(OfficerSlot.Commander, "m2");
            assigned.Should().BeFalse();

            await repository.SaveAsync(rehydrated, CancellationToken.None);

            var reloaded = await repository.GetByIdAsync("g1", CancellationToken.None);

            reloaded.Should().NotBeNull();
            reloaded!.GetOfficerAssignment(OfficerSlot.Commander).Should().Be("m1");
        }

        [Fact]
        public async Task Should_Return_Null_When_Guild_Is_Missing()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"guild-officers-{Guid.NewGuid():N}.db");
            using var fixture = new RepositoryFixture(dbPath);
            var repository = fixture.Repository;

            var result = await repository.GetByIdAsync("missing", CancellationToken.None);

            result.Should().BeNull();
        }
    }
}
