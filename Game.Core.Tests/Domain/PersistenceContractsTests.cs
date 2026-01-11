using System;
using FluentAssertions;
using Game.Core.Contracts.Persistence;
using Xunit;

namespace Game.Core.Tests.Domain;

public class PersistenceContractsTests
{
    [Fact]
    public void SaveRequested_EventType_Should_Match_Expected()
    {
        SaveRequested.EventType.Should().Be("core.save.requested");
    }

    [Fact]
    public void SaveCompleted_EventType_Should_Match_Expected()
    {
        SaveCompleted.EventType.Should().Be("core.save.completed");
    }

    [Fact]
    public void SaveFailed_EventType_Should_Match_Expected()
    {
        SaveFailed.EventType.Should().Be("core.save.failed");
    }

    [Fact]
    public void LoadRequested_EventType_Should_Match_Expected()
    {
        LoadRequested.EventType.Should().Be("core.load.requested");
    }

    [Fact]
    public void LoadCompleted_EventType_Should_Match_Expected()
    {
        LoadCompleted.EventType.Should().Be("core.load.completed");
    }

    [Fact]
    public void LoadFailed_EventType_Should_Match_Expected()
    {
        LoadFailed.EventType.Should().Be("core.load.failed");
    }

    [Fact]
    public void StorageMigrationApplied_EventType_Should_Match_Expected()
    {
        StorageMigrationApplied.EventType.Should().Be("core.storage.migration.applied");
    }

    [Fact]
    public void SaveRequested_Should_Accept_Valid_Fields()
    {
        var now = DateTimeOffset.UtcNow;
        var evt = new SaveRequested(SaveId: "save-1", RequestedAt: now);
        evt.SaveId.Should().Be("save-1");
        evt.RequestedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LoadFailed_Should_Accept_Valid_Fields()
    {
        var now = DateTimeOffset.UtcNow;
        var evt = new LoadFailed(SaveId: "save-1", FailedAt: now, Reason: "InvalidOperationException");
        evt.SaveId.Should().Be("save-1");
        evt.FailedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        evt.Reason.Should().Be("InvalidOperationException");
    }

    [Fact]
    public void StorageMigrationApplied_Should_Accept_Valid_Fields()
    {
        var now = DateTimeOffset.UtcNow;
        var evt = new StorageMigrationApplied(FromVersion: 1, ToVersion: 2, AppliedAt: now, Description: "init");
        evt.FromVersion.Should().Be(1);
        evt.ToVersion.Should().Be(2);
        evt.AppliedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        evt.Description.Should().Be("init");
    }
}
