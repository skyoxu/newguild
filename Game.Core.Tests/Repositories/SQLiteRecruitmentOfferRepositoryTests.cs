using System;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Domain;
using Game.Core.Ports;
using Game.Core.Repositories;
using Game.Core.Tests.Mocks;
using Xunit;

namespace Game.Core.Tests.Repositories;

public sealed class SQLiteRecruitmentOfferRepositoryTests
{
    [Fact]
    public async Task AddAsync_Should_Persist_And_Query_ById_And_ByGuild()
    {
        var db = new MockSQLiteDatabase();
        var repo = new SQLiteRecruitmentOfferRepository(db);

        var offer = new RecruitmentOffer(
            OfferId: "o1",
            GuildId: "g1",
            CandidateId: "u2",
            Role: GuildRole.Member,
            PresentedAt: DateTimeOffset.Parse("2030-01-01T00:00:00Z"));

        await repo.AddAsync(offer);

        var byId = await repo.GetByIdAsync("o1");
        byId.Should().NotBeNull();
        byId!.GuildId.Should().Be("g1");
        byId.CandidateId.Should().Be("u2");
        byId.Role.Should().Be(GuildRole.Member);

        var byGuild = await repo.GetByGuildAsync("g1");
        byGuild.Should().ContainSingle(o => o.OfferId == "o1");

        var schemaVersion = await db.ExecuteScalarAsync(SqlStatement.WithParameters(
            "SELECT version FROM schema_version WHERE id = @id",
            new System.Collections.Generic.Dictionary<string, object?> { ["@id"] = 1 }));
        Convert.ToInt32(schemaVersion).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task RemoveAsync_Should_Delete_Offer()
    {
        var db = new MockSQLiteDatabase();
        var repo = new SQLiteRecruitmentOfferRepository(db);

        await repo.AddAsync(new RecruitmentOffer(
            OfferId: "o1",
            GuildId: "g1",
            CandidateId: "u2",
            Role: GuildRole.Member,
            PresentedAt: DateTimeOffset.Parse("2030-01-01T00:00:00Z")));

        var removed = await repo.RemoveAsync("o1");
        removed.Should().BeTrue();

        var byId = await repo.GetByIdAsync("o1");
        byId.Should().BeNull();
    }
}
