using Content.Shared._Shitmed.Targeting;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._N14.Pipboy;

[Serializable, NetSerializable]
public sealed class N14PipboyItemEntry
{
    public string    Name      = string.Empty;
    public bool      IsEquipped;
    public bool      IsInHand;
    /// <summary>True when this item is stored inside a bag/container slot.</summary>
    public bool      IsInBag;
    /// <summary>Net-entity used for pickup interaction from the client.</summary>
    public NetEntity ItemId;

    public N14PipboyItemEntry() { }

    public N14PipboyItemEntry(string name, bool isEquipped, bool isInHand, NetEntity itemId, bool isInBag = false)
    {
        Name       = name;
        IsEquipped = isEquipped;
        IsInHand   = isInHand;
        ItemId     = itemId;
        IsInBag    = isInBag;
    }
}

[Serializable, NetSerializable]
public sealed class N14PipboyUpdateState : BoundUserInterfaceState
{
    /// <summary>Total damage taken (0 = full health).</summary>
    public FixedPoint2 CurrentDamage;

    /// <summary>Incap/dead threshold used as "max HP".</summary>
    public FixedPoint2 MaxHp;

    public int CapsCount;

    public Dictionary<TargetBodyPart, TargetIntegrity>? BodyParts;

    /// <summary>Net entity of the player wearing the pipboy (for sprite view).</summary>
    public NetEntity? PlayerEntity;

    public List<N14PipboyItemEntry> Items;
    public List<string>             Notes;

    public N14PipboyUpdateState(
        FixedPoint2 currentDamage,
        FixedPoint2 maxHp,
        int capsCount,
        Dictionary<TargetBodyPart, TargetIntegrity>? bodyParts,
        NetEntity? playerEntity,
        List<N14PipboyItemEntry> items,
        List<string> notes)
    {
        CurrentDamage = currentDamage;
        MaxHp         = maxHp;
        CapsCount     = capsCount;
        BodyParts     = bodyParts;
        PlayerEntity  = playerEntity;
        Items         = items;
        Notes         = notes;
    }
}
