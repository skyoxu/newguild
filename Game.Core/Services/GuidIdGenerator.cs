using System;
using Game.Core.Ports;

namespace Game.Core.Services;

public sealed class GuidIdGenerator : IIdGenerator
{
    public string NewId() => Guid.NewGuid().ToString("N");
}

