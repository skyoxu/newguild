using System;
using System.Threading.Tasks;
using Game.Core.Contracts.Raid;
using Game.Core.Ports;
using Game.Core.Services;
using Godot;

namespace Game.Godot.Scripts.Demo;

public sealed class RaidEncounterDemoRunner
{
    private const string ErrorResult = "error";

    private readonly IEventBus _eventBus;
    private readonly ITime _time;
    private readonly IIdGenerator _idGenerator;

    public RaidEncounterDemoRunner(IEventBus eventBus, ITime time, IIdGenerator idGenerator)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<string> RunAsync(int week)
    {
        if (week < 1)
            week = 1;

        var stage = "start";
        try
        {
            var encounterId = $"enc-{week:D3}";
            var sm = new RaidEncounterStateMachine(_time, _idGenerator);
            sm.Start(raidId: "raid-demo", guildId: "guild-demo", week: week, encounterId: encounterId);

            stage = "publish_pending";
            foreach (var evt in sm.DequeueEvents())
                await _eventBus.PublishAsync(evt);

            stage = "advance";
            if (!sm.Advance() || !sm.Advance() || !sm.Advance())
                return ErrorResult;

            stage = "publish_resolve";
            var events = sm.DequeueEvents();
            foreach (var evt in events)
                await _eventBus.PublishAsync(evt);

            stage = "read_result";
            var resolved = events.FindFirstResolved();
            return resolved?.Result ?? ErrorResult;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[RaidEncounterDemo] failed stage={stage} week={week} exType={ex.GetType().Name}");
            if (OS.IsDebugBuild() && string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal))
                GD.PrintErr(ex.ToString());
            return ErrorResult;
        }
    }
}

internal static class RaidEncounterDemoRunnerExtensions
{
    public static RaidResolved? FindFirstResolved(this System.Collections.Generic.IReadOnlyList<Game.Core.Contracts.DomainEvent> events)
    {
        foreach (var evt in events)
        {
            if (evt.Type != RaidResolved.EventType)
                continue;
            if (evt.Data is RaidResolved resolved)
                return resolved;
        }

        return null;
    }
}
