using System;
using Game.Core.Ports;

namespace Game.Core.Services;

public sealed class SystemTime : ITime
{
    public double DeltaSeconds => 0.0;

    public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;
}

