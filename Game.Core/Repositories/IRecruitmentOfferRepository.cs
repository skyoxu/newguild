using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Domain;

namespace Game.Core.Repositories;

/// <summary>
/// Repository interface for recruitment offers persistence.
/// Offers are pending state used by the RecruitmentService; resolved offers may be removed.
/// </summary>
public interface IRecruitmentOfferRepository
{
    Task AddAsync(RecruitmentOffer offer);
    Task<bool> RemoveAsync(string offerId);
    Task<RecruitmentOffer?> GetByIdAsync(string offerId);
    Task<IReadOnlyList<RecruitmentOffer>> GetByGuildAsync(string guildId);
}

