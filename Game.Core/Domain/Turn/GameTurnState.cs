using System;

namespace Game.Core.Domain.Turn;

public sealed record GameTurnState(
    int Week,
    GameTurnPhase Phase,
    string SaveId,
    DateTimeOffset CurrentTime
);

