using System;
using Game.Core.Ports;

namespace Game.Core.Tests.TestDoubles;

public sealed class FixedTime : ITime
{
    private readonly DateTimeOffset _now;

    public FixedTime(DateTimeOffset now) => _now = now;

    public double DeltaSeconds => 1.0 / 60.0;
    public DateTimeOffset UtcNowOffset => _now;
}

