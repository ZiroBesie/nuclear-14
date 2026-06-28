using Content.Server.Hands.Systems;
using Content.Shared._N14.BarterNpc;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.Server._N14.BarterNpc;

public sealed class BarterNpcSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BarterNpcComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<BarterNpcComponent, BarterExecuteMessage>(OnExecuteTrade);
    }

    private void OnUiOpened(EntityUid uid, BarterNpcComponent comp, BoundUIOpenedEvent args)
    {
        SendState(uid, comp);
    }

    private void SendState(EntityUid uid, BarterNpcComponent comp)
    {
        var states = new List<BarterEntryState>();
        foreach (var trade in comp.Trades)
        {
            states.Add(new BarterEntryState(
                trade.GiveItem.Id,
                trade.GiveCount,
                trade.ReceiveItem.Id,
                trade.ReceiveCount));
        }
        _ui.SetUiState(uid, BarterNpcUiKey.Key, new BarterNpcBoundUiState(states));
    }

    private void OnExecuteTrade(EntityUid uid, BarterNpcComponent comp, BarterExecuteMessage args)
    {
        var player = args.Actor;

        if (args.TradeIndex < 0 || args.TradeIndex >= comp.Trades.Count)
            return;

        var trade = comp.Trades[args.TradeIndex];

        // Ищем нужное количество айтемов у игрока
        var found = FindItems(player, trade.GiveItem.Id, trade.GiveCount);
        if (found.Count < trade.GiveCount)
        {
            _popup.PopupEntity(
                Loc.GetString("barter-npc-not-enough", ("item", trade.GiveItem.Id), ("count", trade.GiveCount)),
                player, player);
            return;
        }

        // Удаляем отданные айтемы
        foreach (var ent in found)
        {
            _containers.TryRemoveFromContainer(ent);
            QueueDel(ent);
        }

        // Спавним полученные айтемы в руки / на пол
        var xform = Transform(player);
        for (var i = 0; i < trade.ReceiveCount; i++)
        {
            var spawned = Spawn(trade.ReceiveItem.Id, xform.Coordinates);
            _hands.TryPickupAnyHand(player, spawned);
        }

        _popup.PopupEntity(
            Loc.GetString("barter-npc-success",
                ("give", trade.GiveItem.Id),
                ("receive", trade.ReceiveItem.Id)),
            player, player, PopupType.Medium);
    }

    /// <summary>Ищет до <paramref name="count"/> штук айтема <paramref name="protoId"/> в инвентаре и руках игрока.</summary>
    private List<EntityUid> FindItems(EntityUid player, string protoId, int count)
    {
        var result = new List<EntityUid>();

        // Руки
        if (TryComp<HandsComponent>(player, out var hands))
        {
            foreach (var hand in hands.Hands.Values)
            {
                if (hand.HeldEntity is not { } held) continue;
                if (MetaData(held).EntityPrototype?.ID != protoId) continue;
                result.Add(held);
                if (result.Count >= count) return result;
            }
        }

        // Инвентарь
        if (_inventory.TryGetContainerSlotEnumerator(player, out var slots))
        {
            while (slots.MoveNext(out var slot))
            {
                foreach (var contained in slot.ContainedEntities)
                {
                    if (MetaData(contained).EntityPrototype?.ID != protoId) continue;
                    result.Add(contained);
                    if (result.Count >= count) return result;
                }
            }
        }

        return result;
    }
}
