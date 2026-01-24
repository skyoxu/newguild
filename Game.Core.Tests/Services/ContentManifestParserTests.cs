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
    private const int MaxFileNameChars = 128;

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    // ACC:T27.2
    [Fact]
    public void Should_Parse_ValidManifest_FilesSchema_And_ReturnExpectedEntries()
    {
        var json = BuildFilesManifestJson(
            packId: "Base",
            contentVersion: "1.0.0",
            files: new[] { "guild_events.json", "tuning.json" });

        var manifest = ContentManifestParser.Parse(json);

        manifest.Should().NotBeNull();
        manifest.ManifestId.Should().Be("Base");
        manifest.SchemaVersion.Should().Be("1.0.0");

        manifest.Entries.Should().HaveCount(2);

        var ids = manifest.Entries.Select(e => e.Id).ToArray();
        ids.Should().BeEquivalentTo(new[] { "guild_events", "tuning" });

        var paths = manifest.Entries.Select(e => e.ResourcePath).ToArray();
        paths.Should().BeEquivalentTo(new[]
        {
            "res://Game.Godot/Assets/Data/content/base/guild_events.json",
            "res://Game.Godot/Assets/Data/content/base/tuning.json",
        });
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    // ACC:T27.2
    [Fact]
    public void Should_Accept_SnakeCase_RootFields()
    {
        var json = "{\"pack_id\":\"Base\",\"content_version\":\"1.0.0\",\"files\":[\"tuning.json\"]}";

        var manifest = ContentManifestParser.Parse(json);

        manifest.ManifestId.Should().Be("Base");
        manifest.SchemaVersion.Should().Be("1.0.0");
        manifest.Entries.Should().HaveCount(1);
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    // ACC:T27.2
    [Fact]
    public void Should_Parse_LegacyEntriesSchema_And_ReturnExpectedEntries()
    {
        var json = BuildManifestJson(
            manifestId: "core",
            schemaVersion: "1",
            entries: new[] { new ManifestEntryJson("data.items", "json", "res://Assets/Data/items.json") });

        var manifest = ContentManifestParser.Parse(json);

        manifest.ManifestId.Should().Be("core");
        manifest.SchemaVersion.Should().Be("1");
        manifest.Entries.Should().HaveCount(1);
        manifest.Entries[0].Kind.Should().Be("json");
        manifest.Entries[0].Id.Should().Be("data.items");
        manifest.Entries[0].ResourcePath.Should().Be("res://Assets/Data/items.json");
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    // ACC:T27.2
    [Fact]
    public void Should_Reject_DuplicateEntryIds()
    {
        var json = BuildFilesManifestJson(
            packId: "Base",
            contentVersion: "1.0.0",
            files: new[] { "dup.json", "dup.json" });

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
            entries: new[] { new ManifestEntryJson("unsafe", "json", path) });

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
    [Theory]
    [InlineData("")]
    [InlineData("a.txt")]
    [InlineData("dir/a.json")]
    [InlineData("../a.json")]
    [InlineData("C:/a.json")]
    public void Should_Reject_InvalidFileEntry(string file)
    {
        var json = BuildFilesManifestJson(
            packId: "Base",
            contentVersion: "1.0.0",
            files: new[] { file });

        Action act = () => ContentManifestParser.Parse(json);

        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_FileEntry_TooLong()
    {
        var file = new string('a', MaxFileNameChars + 1) + ".json";
        file.Length.Should().BeGreaterThan(MaxFileNameChars);

        var json = BuildFilesManifestJson(
            packId: "Base",
            contentVersion: "1.0.0",
            files: new[] { file });

        Action act = () => ContentManifestParser.Parse(json);

        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_FileEntry_NotStringOrObject()
    {
        var json = "{\"packId\":\"Base\",\"contentVersion\":\"1.0.0\",\"files\":[1]}";

        Action act = () => ContentManifestParser.Parse(json);

        act.Should().Throw<FormatException>();
    }

    // References: ADR-0004-event-bus-and-contracts (Accepted)
    [Fact]
    public void Should_Reject_TooManyEntries()
    {
        var files = Enumerable.Range(0, MaxEntries + 1)
            .Select(i => $"file{i}.json")
            .ToArray();

        var json = BuildFilesManifestJson(
            packId: "Base",
            contentVersion: "1.0.0",
            files: files);

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
            entries: new[] { new ManifestEntryJson(new string('a', MaxIdChars + 1), "json", "res://Assets/Data/a.json") });

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

    private static string BuildFilesManifestJson(string packId, string contentVersion, IReadOnlyList<string> files)
    {
        var payload = new
        {
            packId,
            contentVersion,
            idNamespacePrefix = "Base_",
            files = files.ToArray(),
        };

        return JsonSerializer.Serialize(payload);
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
