using System;
using System.Threading.Tasks;
using Game.Core.Contracts;
using Game.Core.Contracts.Security;
using Godot;

namespace Game.Godot.Adapters;

internal sealed class SecurityGateDecisionPublisher
{
    private readonly EventBusAdapter _bus;

    public SecurityGateDecisionPublisher(EventBusAdapter bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    public bool TryPublishAiLogPopupDecision(string decision, string reason, string source, string target, string caller)
    {
        try
        {
            var gateDecision = new SecurityAiLogPopupGateDecision(
                Target: target,
                Decision: decision,
                Reason: reason,
                OccurredAt: DateTimeOffset.UtcNow,
                Caller: caller);

            var evt = new DomainEvent(
                Type: SecurityAiLogPopupGateDecision.EventType,
                Source: source,
                Data: gateDecision,
                Timestamp: DateTimeOffset.UtcNow,
                Id: Guid.NewGuid().ToString("N"));

            var publishTask = _bus.PublishAsync(evt);
            _ = publishTask.ContinueWith(task =>
            {
                var exType = task.Exception?.GetBaseException().GetType().Name ?? "Unknown";
                GD.PushWarning($"[SecurityGateDecisionPublisher] publish failed eventType={SecurityAiLogPopupGateDecision.EventType} source={source} reason={reason} target={target} exType={exType}");
            }, TaskContinuationOptions.OnlyOnFaulted);

            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[SecurityGateDecisionPublisher] publish schedule failed eventType={SecurityAiLogPopupGateDecision.EventType} source={source} reason={reason} target={target} exType={ex.GetType().Name}");
            return false;
        }
    }
}
