using System.Threading.Tasks;

namespace Game.Core.Contracts.Achievements;

/// <summary>
/// Port for loading and saving achievement tracker state by save id.
/// </summary>
public interface IAchievementStateStore
{
    /// <summary>
    /// Loads persisted achievement state for the specified save id.
    /// </summary>
    /// <param name="saveId">Validated logical save identifier.</param>
    /// <returns>
    /// Persisted snapshot when present; otherwise <c>null</c>.
    /// </returns>
    Task<AchievementStateSnapshot?> LoadAsync(string saveId);

    /// <summary>
    /// Persists achievement state for the specified save id.
    /// </summary>
    /// <param name="saveId">Validated logical save identifier.</param>
    /// <param name="snapshot">Snapshot to persist.</param>
    Task SaveAsync(string saveId, AchievementStateSnapshot snapshot);
}

