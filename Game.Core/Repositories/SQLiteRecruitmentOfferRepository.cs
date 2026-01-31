using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Domain;
using Game.Core.Persistence.Migrations;
using Game.Core.Ports;

namespace Game.Core.Repositories;

/// <summary>
/// SQLite implementation for recruitment offers.
/// Shares schema_version with the Guild DB (see GuildDbSchema).
/// </summary>
public sealed class SQLiteRecruitmentOfferRepository : IRecruitmentOfferRepository
{
    private readonly ISQLiteDatabase _db;
    private bool _initialized;

    public SQLiteRecruitmentOfferRepository(ISQLiteDatabase db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _db.OpenAsync().ConfigureAwait(false);
        await SchemaMigrationRunner.EnsureLatestAsync(_db, GuildDbSchema.LatestVersion, GuildDbSchema.CreateMigrations())
            .ConfigureAwait(false);
        await GuildDbSchema.EnsureTablesExistAsync(_db).ConfigureAwait(false);

        _initialized = true;
    }

    public async Task AddAsync(RecruitmentOffer offer)
    {
        if (offer is null) throw new ArgumentNullException(nameof(offer));
        await EnsureInitializedAsync().ConfigureAwait(false);

        await _db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
            "INSERT INTO RecruitmentOffers (OfferId, GuildId, CandidateId, Role, PresentedAt) VALUES (@OfferId, @GuildId, @CandidateId, @Role, @PresentedAt)",
            new Dictionary<string, object?>
            {
                ["@OfferId"] = offer.OfferId,
                ["@GuildId"] = offer.GuildId,
                ["@CandidateId"] = offer.CandidateId,
                ["@Role"] = (int)offer.Role,
                ["@PresentedAt"] = offer.PresentedAt.ToString("o"),
            })).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(string offerId)
    {
        if (string.IsNullOrWhiteSpace(offerId)) return false;
        await EnsureInitializedAsync().ConfigureAwait(false);

        var affected = await _db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
            "DELETE FROM RecruitmentOffers WHERE OfferId = @OfferId",
            new Dictionary<string, object?> { ["@OfferId"] = offerId.Trim() })).ConfigureAwait(false);

        return affected > 0;
    }

    public async Task<RecruitmentOffer?> GetByIdAsync(string offerId)
    {
        if (string.IsNullOrWhiteSpace(offerId)) return null;
        await EnsureInitializedAsync().ConfigureAwait(false);

        var rows = await _db.QueryAsync(SqlStatement.WithParameters(
            "SELECT OfferId, GuildId, CandidateId, Role, PresentedAt FROM RecruitmentOffers WHERE OfferId = @OfferId",
            new Dictionary<string, object?> { ["@OfferId"] = offerId.Trim() })).ConfigureAwait(false);

        if (rows.Count == 0) return null;
        return Map(rows[0]);
    }

    public async Task<IReadOnlyList<RecruitmentOffer>> GetByGuildAsync(string guildId)
    {
        if (string.IsNullOrWhiteSpace(guildId)) return Array.Empty<RecruitmentOffer>();
        await EnsureInitializedAsync().ConfigureAwait(false);

        var rows = await _db.QueryAsync(SqlStatement.WithParameters(
            "SELECT OfferId, GuildId, CandidateId, Role, PresentedAt FROM RecruitmentOffers WHERE GuildId = @GuildId",
            new Dictionary<string, object?> { ["@GuildId"] = guildId.Trim() })).ConfigureAwait(false);

        var offers = new List<RecruitmentOffer>(rows.Count);
        foreach (var row in rows)
            offers.Add(Map(row));

        return offers;
    }

    private static RecruitmentOffer Map(Dictionary<string, object> row)
    {
        var offerId = (string)row["OfferId"];
        var guildId = (string)row["GuildId"];
        var candidateId = (string)row["CandidateId"];

        var roleValue = row["Role"];
        var role = roleValue is long roleLong ? (GuildRole)(int)roleLong : (GuildRole)(int)roleValue;

        var presentedAt = DateTimeOffset.Parse((string)row["PresentedAt"]);
        return new RecruitmentOffer(offerId, guildId, candidateId, role, presentedAt);
    }
}

