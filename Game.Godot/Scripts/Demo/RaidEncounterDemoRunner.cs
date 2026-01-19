using System;
using System.Threading.Tasks;
using Game.Core.Contracts.Raid;
using Game.Core.Ports;
using Game.Core.Services;
using Godot;

namespace Game.Godot.Scripts.Demo;

public sealed record RaidEncounterDemoOutcome(string Result, int RewardPoints);

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

    public RaidEncounterDemoOutcome Run(int week)
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
                ObserveFireAndForget(_eventBus.PublishAsync(evt), "raid_demo_publish_pending");

            stage = "advance";
            if (!sm.Advance() || !sm.Advance() || !sm.Advance())
                return new RaidEncounterDemoOutcome(ErrorResult, RewardPoints: 0);

            stage = "publish_resolve";
            var events = sm.DequeueEvents();
            foreach (var evt in events)
                ObserveFireAndForget(_eventBus.PublishAsync(evt), "raid_demo_publish_resolve");

            stage = "read_result";
            var resolved = events.FindFirstResolved();
            if (resolved == null)
                return new RaidEncounterDemoOutcome(ErrorResult, RewardPoints: 0);
            return new RaidEncounterDemoOutcome(resolved.Result, resolved.RewardPoints);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[RaidEncounterDemo] failed stage={stage} week={week} exType={ex.GetType().Name}");
            if (OS.IsDebugBuild() && string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal))
                GD.PrintErr(ex.ToString());
            return new RaidEncounterDemoOutcome(ErrorResult, RewardPoints: 0);
        }
    }

    public Task<RaidEncounterDemoOutcome> RunAsync(int week) => Task.FromResult(Run(week));

    private static void ObserveFireAndForget(Task task, string label)
    {
        try
        {
            _ = task.ContinueWith(
                t =>
                {
                    var ex = t.Exception?.GetBaseException();
                    if (IsFireAndForgetObservationEnabled())
                        Console.Error.WriteLine($"[RaidEncounterDemoRunner] publish failed label={label} exType={ex?.GetType().Name ?? "unknown"}");
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }
        catch
        {
            // Best-effort only.
        }
    }

    private static bool IsFireAndForgetObservationEnabled()
    {
        if (OS.IsDebugBuild())
            return true;

        return string.Equals(OS.GetEnvironment("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal) ||
               string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal) ||
               string.Equals(System.Environment.GetEnvironmentVariable("CI"), "1", StringComparison.Ordinal) ||
               string.Equals(System.Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
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
