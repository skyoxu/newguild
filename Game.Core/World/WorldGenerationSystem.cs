using System;
using System.Collections.Generic;

namespace Game.Core.World;

/// <summary>
/// World generation system for new game initialization.
/// Owns the start seed as the single source of truth (Task 40).
/// </summary>
public sealed class WorldGenerationSystem
{
    /// <summary>
    /// The raw seed string used for deterministic world generation.
    /// </summary>
    public string Seed { get; }

    public WorldGenerationSystem(string seed)
    {
        if (string.IsNullOrWhiteSpace(seed))
            throw new ArgumentException("Seed cannot be null or whitespace.", nameof(seed));

        Seed = seed;
    }

    /// <summary>
    /// Generates deterministic NPC guild ids based on the seed.
    /// </summary>
    public IReadOnlyList<string> GenerateNpcGuildIds(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be >= 0.");

        var ids = new List<string>(capacity: count);
        var rng = new XorShift32(HashSeed(Seed));

        for (var i = 0; i < count; i++)
        {
            var value = rng.NextUInt();
            ids.Add($"npc-guild-{value:x8}");
        }

        return ids;
    }

    private static uint HashSeed(string seed)
    {
        // FNV-1a 32-bit hash (stable across runtimes).
        unchecked
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;

            var hash = offsetBasis;
            foreach (var character in seed)
            {
                hash ^= character;
                hash *= prime;
            }

            // Avoid a zero-state PRNG.
            return hash == 0 ? 1u : hash;
        }
    }

    private struct XorShift32
    {
        private uint _state;

        public XorShift32(uint seed)
        {
            _state = seed == 0 ? 1u : seed;
        }

        public uint NextUInt()
        {
            var state = _state;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            _state = state;
            return state;
        }
    }
}
