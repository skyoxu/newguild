using System;
using System.Collections.Generic;
using Game.Core.Ports;

namespace Game.Core.Tests.TestDoubles;

public sealed class SequenceIdGenerator : IIdGenerator
{
    private readonly Queue<string> _ids;

    public SequenceIdGenerator(params string[] ids) =>
        _ids = new Queue<string>(ids);

    public string NewId()
    {
        if (_ids.Count == 0)
            throw new InvalidOperationException("No more ids available");
        return _ids.Dequeue();
    }
}

