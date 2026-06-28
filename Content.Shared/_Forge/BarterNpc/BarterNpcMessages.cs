using Robust.Shared.Serialization;

namespace Content.Shared._N14.BarterNpc;

/// <summary>Состояние UI, отправляемое клиенту.</summary>
[Serializable, NetSerializable]
public sealed class BarterNpcBoundUiState : BoundUserInterfaceState
{
    public List<BarterEntryState> Trades;

    public BarterNpcBoundUiState(List<BarterEntryState> trades)
    {
        Trades = trades;
    }
}

[Serializable, NetSerializable]
public sealed class BarterEntryState
{
    public string GiveItem;
    public int GiveCount;
    public string ReceiveItem;
    public int ReceiveCount;

    public BarterEntryState(string giveItem, int giveCount, string receiveItem, int receiveCount)
    {
        GiveItem = giveItem;
        GiveCount = giveCount;
        ReceiveItem = receiveItem;
        ReceiveCount = receiveCount;
    }
}

/// <summary>Клиент хочет совершить обмен под индексом TradeIndex.</summary>
[Serializable, NetSerializable]
public sealed class BarterExecuteMessage : BoundUserInterfaceMessage
{
    public int TradeIndex;

    public BarterExecuteMessage(int tradeIndex)
    {
        TradeIndex = tradeIndex;
    }
}
