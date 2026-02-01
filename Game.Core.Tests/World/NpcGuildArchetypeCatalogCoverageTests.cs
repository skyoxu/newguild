#nullable enable

using System;
using System.Linq;
using FluentAssertions;
using Game.Core.World;
using Xunit;

namespace Game.Core.Tests.World;

public sealed class NpcGuildArchetypeCatalogCoverageTests
{
    [Fact]
    public void LoadFromContentJson_WhenOverlaySchemaPropertyName_ShouldLoadArchetypes()
    {
        var catalog = NpcGuildArchetypeCatalog.LoadFromContentJson("""
{
  "contentVersion": "1",
  "npcGuildArchetypes": [
    { "id": "merchant_guild" }
  ]
}
""");

        catalog.ContentVersion.Should().Be("1");
        catalog.TryGetById("merchant_guild", out var archetype).Should().BeTrue();
        archetype.Should().NotBeNull();
        archetype!.Id.Should().Be("merchant_guild");
    }

    [Fact]
    public void LoadFromContentJson_WhenContentVersionUsesSnakeCase_ShouldSetContentVersion()
    {
        var catalog = NpcGuildArchetypeCatalog.LoadFromContentJson("""
{
  "content_version": "1.2",
  "npcGuildArchetypes": [
    { "id": "npc.guild-1" }
  ]
}
""");

        catalog.ContentVersion.Should().Be("1.2");
        catalog.Count.Should().Be(1);
    }

    [Fact]
    public void LoadFromContentJson_WhenContentVersionIsNotString_ShouldThrowArgumentException()
    {
        Action action = () => NpcGuildArchetypeCatalog.LoadFromContentJson("""
{
  "contentVersion": 1,
  "npcGuildArchetypes": [
    { "id": "merchant_guild" }
  ]
}
""");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*must be a string*");
    }

    [Fact]
    public void LoadFromContentJson_WhenArrayPropertyMissing_ShouldThrowArgumentException()
    {
        Action action = () => NpcGuildArchetypeCatalog.LoadFromContentJson("""{ "contentVersion": "1" }""");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*missing*");
    }

    [Fact]
    public void LoadFromContentJson_WhenDuplicateId_ShouldThrowArgumentException()
    {
        Action action = () => NpcGuildArchetypeCatalog.LoadFromContentJson("""
{
  "npcGuildArchetypes": [
    { "id": "dup" },
    { "id": "dup" }
  ]
}
""");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Duplicate*");
    }

    [Fact]
    public void TryGetById_WhenIdIsWhitespace_ShouldReturnFalse()
    {
        var catalog = NpcGuildArchetypeCatalog.LoadFromContentJson("""
{
  "npcGuildArchetypes": [
    { "id": "merchant_guild" }
  ]
}
""");

        catalog.TryGetById(" ", out var archetype).Should().BeFalse();
        archetype.Should().BeNull();
    }

    [Fact]
    public void LoadFromContentJson_WhenItemsContainNonObjectEntries_ShouldIgnoreThoseEntries()
    {
        var catalog = NpcGuildArchetypeCatalog.LoadFromContentJson("""
{
  "npcGuildArchetypes": [
    { "id": "merchant_guild" },
    "invalid"
  ]
}
""");

        catalog.ToArray().Should().HaveCount(1);
    }

    [Fact]
    public void LoadFromContentJson_WhenJsonIsNull_ShouldThrowArgumentNullException()
    {
        Action action = () => NpcGuildArchetypeCatalog.LoadFromContentJson(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LoadFromContentJson_WhenJsonIsInvalid_ShouldThrowArgumentException()
    {
        Action action = () => NpcGuildArchetypeCatalog.LoadFromContentJson("""{ "npcGuildArchetypes": [ }""");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*invalid*");
    }

    [Fact]
    public void LoadFromContentJson_WhenRootIsNotObject_ShouldThrowArgumentException()
    {
        Action action = () => NpcGuildArchetypeCatalog.LoadFromContentJson("""[]""");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*root*");
    }

    [Fact]
    public void LoadFromContentJson_WhenItemsPropertyIsNotArray_ShouldThrowArgumentException()
    {
        Action action = () => NpcGuildArchetypeCatalog.LoadFromContentJson("""{ "npcGuildArchetypes": {} }""");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*must be an array*");
    }

    [Fact]
    public void TryGetById_WhenIdMissing_ShouldReturnFalse()
    {
        var catalog = NpcGuildArchetypeCatalog.LoadFromContentJson("""
{
  "npcGuildArchetypes": [
    { "id": "merchant_guild" }
  ]
}
""");

        catalog.TryGetById("not_found", out var archetype).Should().BeFalse();
        archetype.Should().BeNull();
    }

    [Fact]
    public void LoadFromContentJson_WhenItemMissingId_ShouldThrowArgumentException()
    {
        Action action = () => NpcGuildArchetypeCatalog.LoadFromContentJson("""
{
  "npcGuildArchetypes": [
    { "nameKey": "missing_id" }
  ]
}
""");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*missing*");
    }

    [Fact]
    public void LoadFromContentJson_WhenIdContainsWhitespace_ShouldThrowArgumentException()
    {
        Action action = () => NpcGuildArchetypeCatalog.LoadFromContentJson("""
{
  "npcGuildArchetypes": [
    { "id": "merchant guild" }
  ]
}
""");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*invalid characters*");
    }

    [Fact]
    public void LoadFromContentJson_WhenIdContainsSlash_ShouldThrowArgumentException()
    {
        Action action = () => NpcGuildArchetypeCatalog.LoadFromContentJson("""
{
  "npcGuildArchetypes": [
    { "id": "merchant/guild" }
  ]
}
""");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*invalid characters*");
    }
}
