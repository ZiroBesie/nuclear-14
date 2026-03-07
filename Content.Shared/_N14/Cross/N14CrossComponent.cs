using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._N14.Cross;

[RegisterComponent]
public sealed partial class N14CrossComponent : Component
{
    [DataField("hangDelay")]
    public TimeSpan HangDelay = TimeSpan.FromSeconds(20);

    [DataField("unhangDelay")]
    public TimeSpan UnhangDelay = TimeSpan.FromSeconds(20);

    [DataField("restraintPrototype")]
    public EntProtoId RestraintPrototype = "N14CrossRestraints";

    [DataField("northBuckleOffset")]
    public Vector2 NorthBuckleOffset = new(0.08f, 0.25f);

    [DataField("southBuckleOffset")]
    public Vector2 SouthBuckleOffset = new(0.03f, 0.23f);

    [DataField("eastBuckleOffset")]
    public Vector2 EastBuckleOffset = new(0.24f, 0.22f);

    [DataField("westBuckleOffset")]
    public Vector2 WestBuckleOffset = new(-0.24f, 0.22f);

    [ViewVariables]
    public bool Busy;

    [ViewVariables]
    public TimeSpan? BusyUntil;

    [ViewVariables]
    public bool BypassNextHangAttempt;

    [ViewVariables]
    public bool BypassNextUnhangAttempt;

    [ViewVariables]
    public bool AllowNextUnstrap;

    public Vector2 GetBuckleOffset(Direction direction, Vector2 fallback)
    {
        return direction switch
        {
            Direction.North => NorthBuckleOffset,
            Direction.South => SouthBuckleOffset,
            Direction.East => EastBuckleOffset,
            Direction.West => WestBuckleOffset,
            _ => fallback
        };
    }
}

[Serializable, NetSerializable]
public sealed partial class N14CrossHangDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity HangTarget;
}

[Serializable, NetSerializable]
public sealed partial class N14CrossUnhangDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity UnhangTarget;
}

