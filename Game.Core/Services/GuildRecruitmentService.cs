using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Contracts;
using Game.Core.Contracts.Recruitment;
using Game.Core.Domain;
using Game.Core.Ports;
using Game.Core.Repositories;

namespace Game.Core.Services;

public sealed class GuildRecruitmentService
{
    private readonly IRecruitmentOfferRepository _offerRepository;
    private readonly IEventBus _eventBus;
    private readonly GuildRosterService _rosterService;
    private readonly ILogger _logger;

    private readonly Dictionary<string, Offer> _offersById = new(StringComparer.Ordinal);
    private readonly Dictionary<(string GuildId, string CandidateId), string> _offerIdByGuildCandidate = new();
    private readonly HashSet<string> _loadedGuilds = new(StringComparer.Ordinal);

    private sealed record Offer(string OfferId, string GuildId, string CandidateId, GuildRole Role);

    public GuildRecruitmentService(
        IGuildRepository guildRepository,
        IRecruitmentOfferRepository offerRepository,
        IEventBus eventBus,
        GuildRosterService rosterService,
        ITime time,
        ILogger logger,
        IEventCatalog eventCatalog)
    {
        _offerRepository = offerRepository ?? throw new ArgumentNullException(nameof(offerRepository));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _rosterService = rosterService ?? throw new ArgumentNullException(nameof(rosterService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async Task EnsureLoadedAsync(string guildId)
    {
        if (string.IsNullOrWhiteSpace(guildId))
            return;

        if (_loadedGuilds.Contains(guildId))
            return;

        var offers = await _offerRepository.GetByGuildAsync(guildId).ConfigureAwait(false);
        foreach (var o in offers)
        {
            var offer = new Offer(o.OfferId, o.GuildId, o.CandidateId, o.Role);
            _offersById[o.OfferId] = offer;
            _offerIdByGuildCandidate[(o.GuildId, o.CandidateId)] = o.OfferId;
        }

        _loadedGuilds.Add(guildId);
    }

    public async Task ApplyAsync(Guild guild, string candidateId, string role, DateTimeOffset appliedAt)
    {
        if (guild == null) throw new ArgumentNullException(nameof(guild));

        if (string.IsNullOrWhiteSpace(candidateId))
        {
            _logger.Warn("Recruitment apply rejected: candidateId is empty.");
            return;
        }
        candidateId = candidateId.Trim();

        if (!TryParseRole(role, out var parsedRole, out var normalizedRole))
        {
            _logger.Warn("Recruitment apply rejected: invalid role.");
            return;
        }

        await EnsureLoadedAsync(guild.GuildId).ConfigureAwait(false);

        var key = (guild.GuildId, candidateId);
        if (_offerIdByGuildCandidate.ContainsKey(key))
        {
            _logger.Warn("Recruitment apply rejected: duplicate offer.");
            return;
        }

        var offerId = Guid.NewGuid().ToString("N");
        var offer = new Offer(offerId, guild.GuildId, candidateId, parsedRole);

        try
        {
            await _offerRepository.AddAsync(new RecruitmentOffer(offerId, guild.GuildId, candidateId, parsedRole, appliedAt))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Recruitment apply rejected: persist failed exType={ex.GetType().Name}");
            return;
        }

        _offersById[offerId] = offer;
        _offerIdByGuildCandidate[key] = offerId;

        var evt = new RecruitmentOfferPresented(
            OfferId: offerId,
            GuildId: guild.GuildId,
            CandidateId: candidateId,
            Role: normalizedRole,
            PresentedAt: appliedAt);

        await _eventBus.PublishAsync(ToDomainEvent(RecruitmentOfferPresented.EventType, evt, appliedAt))
            .ConfigureAwait(false);
    }

    public async Task ApproveAsync(Guild guild, string offerId, string approvedByUserId, DateTimeOffset resolvedAt)
    {
        if (guild == null) throw new ArgumentNullException(nameof(guild));

        await EnsureLoadedAsync(guild.GuildId).ConfigureAwait(false);

        if (!TryGetOffer(offerId, guild, out var offer))
        {
            _logger.Warn("Recruitment approve rejected: offer not found.");
            return;
        }

        var joinOk = await _rosterService
            .JoinAsync(guild, offer.CandidateId, offer.Role, approvedByUserId, resolvedAt)
            .ConfigureAwait(false);

        if (!joinOk)
        {
            _logger.Warn("Recruitment approve rejected: join failed.");
            return;
        }

        var resolved = new RecruitmentOfferResolved(
            OfferId: offer.OfferId,
            GuildId: offer.GuildId,
            CandidateId: offer.CandidateId,
            Decision: "accepted",
            Reason: "approved",
            ResolvedAt: resolvedAt);

        await _eventBus.PublishAsync(ToDomainEvent(RecruitmentOfferResolved.EventType, resolved, resolvedAt))
            .ConfigureAwait(false);

        await RemoveOfferAsync(offer).ConfigureAwait(false);
    }

    public async Task RejectAsync(Guild guild, string offerId, string rejectedByUserId, string reason, DateTimeOffset resolvedAt)
    {
        if (guild == null) throw new ArgumentNullException(nameof(guild));

        await EnsureLoadedAsync(guild.GuildId).ConfigureAwait(false);

        if (!TryGetOffer(offerId, guild, out var offer))
        {
            _logger.Warn("Recruitment reject ignored: offer not found.");
            return;
        }

        var resolved = new RecruitmentOfferResolved(
            OfferId: offer.OfferId,
            GuildId: offer.GuildId,
            CandidateId: offer.CandidateId,
            Decision: "rejected",
            Reason: string.IsNullOrWhiteSpace(reason) ? "rejected" : reason.Trim(),
            ResolvedAt: resolvedAt);

        await _eventBus.PublishAsync(ToDomainEvent(RecruitmentOfferResolved.EventType, resolved, resolvedAt))
            .ConfigureAwait(false);

        await RemoveOfferAsync(offer).ConfigureAwait(false);
    }

    public async Task WithdrawAsync(Guild guild, string offerId, string candidateId, DateTimeOffset resolvedAt)
    {
        if (guild == null) throw new ArgumentNullException(nameof(guild));

        await EnsureLoadedAsync(guild.GuildId).ConfigureAwait(false);

        if (!TryGetOffer(offerId, guild, out var offer))
        {
            _logger.Warn("Recruitment withdraw rejected: offer not found.");
            return;
        }

        candidateId = candidateId?.Trim() ?? string.Empty;
        if (!string.Equals(offer.CandidateId, candidateId, StringComparison.Ordinal))
        {
            _logger.Warn("Recruitment withdraw rejected: candidate mismatch.");
            return;
        }

        var resolved = new RecruitmentOfferResolved(
            OfferId: offer.OfferId,
            GuildId: offer.GuildId,
            CandidateId: offer.CandidateId,
            Decision: "withdrawn",
            Reason: "withdrawn",
            ResolvedAt: resolvedAt);

        await _eventBus.PublishAsync(ToDomainEvent(RecruitmentOfferResolved.EventType, resolved, resolvedAt))
            .ConfigureAwait(false);

        await RemoveOfferAsync(offer).ConfigureAwait(false);
    }

    private static DomainEvent ToDomainEvent(string type, object data, DateTimeOffset ts) =>
        new(
            Type: type,
            Source: nameof(GuildRecruitmentService),
            Data: data,
            Timestamp: ts.UtcDateTime,
            Id: Guid.NewGuid().ToString("N"));

    private bool TryGetOffer(string offerId, Guild guild, out Offer offer)
    {
        offer = null!;
        if (string.IsNullOrWhiteSpace(offerId))
            return false;
        var trimmedOfferId = offerId.Trim();
        if (!_offersById.TryGetValue(trimmedOfferId, out var found))
            return false;
        if (!string.Equals(found.GuildId, guild.GuildId, StringComparison.Ordinal))
            return false;

        offer = found;
        return true;
    }

    private async Task RemoveOfferAsync(Offer offer)
    {
        _offersById.Remove(offer.OfferId);
        _offerIdByGuildCandidate.Remove((offer.GuildId, offer.CandidateId));
        await _offerRepository.RemoveAsync(offer.OfferId).ConfigureAwait(false);
    }

    private static bool TryParseRole(string role, out GuildRole parsed, out string normalizedRole)
    {
        parsed = default;
        normalizedRole = string.Empty;

        if (string.IsNullOrWhiteSpace(role))
            return false;

        var trimmedRole = role.Trim();
        if (string.Equals(trimmedRole, "member", StringComparison.OrdinalIgnoreCase))
        {
            parsed = GuildRole.Member;
            normalizedRole = "member";
            return true;
        }

        if (string.Equals(trimmedRole, "admin", StringComparison.OrdinalIgnoreCase))
        {
            parsed = GuildRole.Admin;
            normalizedRole = "admin";
            return true;
        }

        return false;
    }
}
