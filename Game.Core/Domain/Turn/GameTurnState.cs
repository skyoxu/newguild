using System;

namespace Game.Core.Domain.Turn;

public sealed record GameTurnState(
    int Week,
    GameTurnPhase Phase,
    SaveIdValue SaveId,
    DateTimeOffset CurrentTime
);

