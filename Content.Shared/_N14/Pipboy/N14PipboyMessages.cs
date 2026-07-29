using Robust.Shared.Serialization;

namespace Content.Shared._N14.Pipboy;

[Serializable, NetSerializable]
public sealed class N14PipboyRequestUpdateMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class N14PipboyAddNoteMessage : BoundUserInterfaceMessage
{
    public readonly string Text;
    public N14PipboyAddNoteMessage(string text) => Text = text;
}

[Serializable, NetSerializable]
public sealed class N14PipboyDeleteNoteMessage : BoundUserInterfaceMessage
{
    public readonly int Index;
    public N14PipboyDeleteNoteMessage(int index) => Index = index;
}

[Serializable, NetSerializable]
public sealed class N14PipboyRadioSelectMessage : BoundUserInterfaceMessage
{
    public readonly string SongId;
    public N14PipboyRadioSelectMessage(string songId) => SongId = songId;
}

[Serializable, NetSerializable]
public sealed class N14PipboyRadioPlayMessage : BoundUserInterfaceMessage
{
    public readonly bool Play;
    public N14PipboyRadioPlayMessage(bool play) => Play = play;
}

[Serializable, NetSerializable]
public sealed class N14PipboyRadioStopMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class N14PipboyRadioSetTimeMessage : BoundUserInterfaceMessage
{
    public readonly float Time;
    public N14PipboyRadioSetTimeMessage(float time) => Time = time;
}

/// <summary>
/// Client requests the server to pick up (or put in hand) an inventory item.
/// </summary>
[Serializable, NetSerializable]
public sealed class N14PipboyPickupItemMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity ItemId;
    public N14PipboyPickupItemMessage(NetEntity itemId) => ItemId = itemId;
}
