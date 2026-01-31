using System;
using System.Collections.Generic;
using Game.Core.Contracts;
using Game.Core.Services;
using Xunit;
using System.Threading.Tasks;

namespace Game.Core.Tests.Services;

public class EventBusTests
{
    [Fact]
    public async Task Publish_Invokes_Subscribers_And_Unsubscribe_Works()
    {
        var bus = new InMemoryEventBus();
        int called = 0;
        var sub = bus.Subscribe(async e => { called++; await Task.CompletedTask; });

        await bus.PublishAsync(new DomainEvent(
            Type: "test.evt",
            Source: nameof(EventBusTests),
            Data: new { ok = true },
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString()
        ));

        Assert.Equal(1, called);
        sub.Dispose();

        await bus.PublishAsync(new DomainEvent(
            Type: "test.evt2",
            Source: nameof(EventBusTests),
            Data: null,
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString()
        ));
        Assert.Equal(1, called);
    }

    [Fact]
    public async Task Subscriber_Exception_Is_Swallowed_And_Others_Still_Called()
    {
        var bus = new InMemoryEventBus();
        int ok = 0;
        bus.Subscribe(_ => throw new InvalidOperationException("boom"));
        bus.Subscribe(_ => { ok++; return Task.CompletedTask; });

        await bus.PublishAsync(new DomainEvent(
            Type: "evt",
            Source: nameof(EventBusTests),
            Data: null,
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString()
        ));
        Assert.Equal(1, ok);
    }

    [Fact]
    public async Task Subscriber_Exception_Is_Logged_And_Dispose_Is_Idempotent()
    {
        var logger = new CapturingLogger();
        var bus = new InMemoryEventBus(logger);
        var sub = bus.Subscribe(_ => throw new InvalidOperationException("boom"));

        await bus.PublishAsync(new DomainEvent(
            Type: "evt",
            Source: nameof(EventBusTests),
            Data: null,
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString()
        ));

        Assert.Single(logger.Errors);
        sub.Dispose();
        sub.Dispose();
    }

    private sealed class CapturingLogger : Game.Core.Ports.ILogger
    {
        public List<string> Errors { get; } = new();
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) => Errors.Add(message);
        public void Error(string message, Exception ex) => Errors.Add(message);
    }
}
