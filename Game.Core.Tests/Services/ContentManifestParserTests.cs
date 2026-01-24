using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Game.Core.Contracts.Content;
using Game.Core.Services.Content;
using Xunit;

namespace Game.Core.Tests.Services;

public sealed class ContentManifestParserTests
{
    private const int MaxManifestJsonChars = 256 * 1024;
    private const int MaxEntries = 1_024;
    private const int MaxIdChars = 128;
    private const int MaxTypeChars = 64;
    private const int MaxPathChars = 512;

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    // ACC:T27.2
    [Fact]
    public void Should_Parse_ValidManifest_And_ReturnExpectedShape()
    {
        var json = BuildManifestJson(
            manifestId: "core",
            schemaVersion: "1",
            entries: new[]
            {
                new ManifestEntryJson("ui.main_menu", "scene", "res://Assets/Data/ui/main_menu.tscn"),
                new ManifestEntryJson("data.items", "json", "res://Assets/Data/items.json"),
            });

        var manifest = ContentManifestParser.Parse(json);

        manifest.Should().NotBeNull();
        manifest.ManifestId.Should().Be("core");
        manifest.SchemaVersion.Should().Be("1");

        manifest.Entries.Should().HaveCount(2);

        var ids = manifest.Entries.Select(e => e.Id).ToArray();
        ids.Should().BeEquivalentTo(new[] { "ui.main_menu", "data.items" });

        var paths = manifest.Entries.Select(e => e.ResourcePath).ToArray();
        paths.Should().BeEquivalentTo(new[]
        {
            "res://Assets/Data/ui/main_menu.tscn",
            "res://Assets/Data/items.json",
        });
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    // ACC:T27.2
    [Fact]
    public void Should_Accept_SnakeCase_RootFields()
    {
        var json =
            "{\"manifest_id\":\"core\",\"schema_version\":\"1\",\"entries\":[{\"id\":\"ok\",\"type\":\"json\",\"path\":\"res://Assets/Data/a.json\"}]}";

        var manifest = ContentManifestParser.Parse(json);

        manifest.ManifestId.Should().Be("core");
        manifest.SchemaVersion.Should().Be("1");
        manifest.Entries.Should().HaveCount(1);
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    // ACC:T27.2
    [Fact]
    public void Should_Reject_DuplicateEntryIds()
    {
        var json = BuildManifestJson(
            manifestId: "core",
            schemaVersion: "1",
            entries: new[]
            {
                new ManifestEntryJson("dup", "json", "res://Assets/Data/a.json"),
                new ManifestEntryJson("dup", "json", "res://Assets/Data/b.json"),
            });

        Action act = () => ContentManifestParser.Parse(json);

        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    // ACC:T27.2
    [Theory]
    [InlineData("../secrets.json")]
    [InlineData("C:/Windows/system.ini")]
    [InlineData("file:///C:/Windows/system.ini")]
    [InlineData("user://data/game.db")]
    public void Should_Reject_UnsafeEntryPath(string path)
    {
        var json = BuildManifestJson(
            manifestId: "core",
            schemaVersion: "1",
            entries: new[]
            {
                new ManifestEntryJson("unsafe", "json", path),
            });

        Action act = () => ContentManifestParser.Parse(json);

        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_InvalidJson()
    {
        Action act = () => ContentManifestParser.Parse("{");
        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_EmptyManifestJson()
    {
        Action act = () => ContentManifestParser.Parse("  \n  ");
        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_ManifestJson_TooLarge()
    {
        var oversized = new string('a', MaxManifestJsonChars + 1);

        Action act = () => ContentManifestParser.Parse(oversized);

        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_Root_NotObject()
    {
        Action act = () => ContentManifestParser.Parse("[]");
        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_MissingEntries()
    {
        var json = "{\"manifestId\":\"core\",\"schemaVersion\":\"1\"}";
        Action act = () => ContentManifestParser.Parse(json);
        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_Entries_NotArray()
    {
        var json = "{\"manifestId\":\"core\",\"schemaVersion\":\"1\",\"entries\":{}}";
        Action act = () => ContentManifestParser.Parse(json);
        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_Entry_NotObject()
    {
        var json = "{\"manifestId\":\"core\",\"schemaVersion\":\"1\",\"entries\":[1]}";
        Action act = () => ContentManifestParser.Parse(json);
        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_TooManyEntries()
    {
        var entries = Enumerable.Range(0, MaxEntries + 1)
            .Select(i => new ManifestEntryJson($"id{i}", "json", "res://Assets/Data/a.json"))
            .ToArray();

        var json = BuildManifestJson(
            manifestId: "core",
            schemaVersion: "1",
            entries: entries);

        Action act = () => ContentManifestParser.Parse(json);

        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_EntryId_TooLong()
    {
        var json = BuildManifestJson(
            manifestId: "core",
            schemaVersion: "1",
            entries: new[]
            {
                new ManifestEntryJson(new string('a', MaxIdChars + 1), "json", "res://Assets/Data/a.json"),
            });

        Action act = () => ContentManifestParser.Parse(json);

        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_EntryType_TooLong()
    {
        var json = BuildManifestJson(
            manifestId: "core",
            schemaVersion: "1",
            entries: new[]
            {
                new ManifestEntryJson("ok", new string('b', MaxTypeChars + 1), "res://Assets/Data/a.json"),
            });

        Action act = () => ContentManifestParser.Parse(json);

        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_EntryPath_TooLong()
    {
        var longPath = "res://" + new string('c', MaxPathChars - "res://".Length + 1);
        longPath.Length.Should().BeGreaterThan(MaxPathChars);

        var json = BuildManifestJson(
            manifestId: "core",
            schemaVersion: "1",
            entries: new[]
            {
                new ManifestEntryJson("ok", "json", longPath),
            });

        Action act = () => ContentManifestParser.Parse(json);

        act.Should().Throw<FormatException>();
    }

    private static string BuildManifestJson(string manifestId, string schemaVersion, IReadOnlyList<ManifestEntryJson> entries)
    {
        var payload = new
        {
            manifestId,
            schemaVersion,
            entries = entries.Select(e => new { id = e.Id, type = e.Type, path = e.Path }).ToArray(),
        };

        return JsonSerializer.Serialize(payload);
    }

    private sealed record ManifestEntryJson(string Id, string Type, string Path);
}
