using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._N14.BarterNpc;

[RegisterComponent, NetworkedComponent]
public sealed partial class BarterNpcComponent : Component
{
    /// <summary>
    /// Список обменов: отдаёшь — получаешь.
    /// </summary>
    [DataField]
    public List<BarterEntry> Trades = new();
}

[DataDefinition]
public sealed partial class BarterEntry
{
    /// <summary>Что игрок должен отдать.</summary>
    [DataField(required: true)]
    public EntProtoId GiveItem = default!;

    [DataField]
    public int GiveCount = 1;

    /// <summary>Что игрок получает взамен.</summary>
    [DataField(required: true)]
    public EntProtoId ReceiveItem = default!;

    [DataField]
    public int ReceiveCount = 1;
}
