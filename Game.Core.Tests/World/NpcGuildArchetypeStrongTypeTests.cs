#nullable enable

using FluentAssertions;
using Game.Core.World;
using Xunit;

namespace Game.Core.Tests.World;

public sealed class NpcGuildArchetypeStrongTypeTests
{
    [Fact]
    public void Should_Load_And_Lookup_Archetypes_Using_StrongTypes()
    {
        var json = """
{
  "npcGuildArchetypes": [
    { "id": "merchant_guild" },
    { "id": "thieves_guild" }
  ]
}
""";

        var catalog = NpcGuildArchetypeCatalog.LoadFromContentJson(json);

        var found = catalog.TryGetById("merchant_guild", out var archetype);
        found.Should().BeTrue();
        archetype.Should().NotBeNull();
        archetype!.Id.Should().Be("merchant_guild");
    }
}

