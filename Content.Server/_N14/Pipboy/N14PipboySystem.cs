using Content.Shared._N14.Pipboy;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Audio.Jukebox;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._N14.Pipboy;

public sealed class N14PipboySystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem    _ui         = default!;
    [Dependency] private readonly SharedContainerSystem  _containers = default!;
    [Dependency] private readonly InventorySystem        _inventory  = default!;
    [Dependency] private readonly SharedBodySystem       _body       = default!;
    [Dependency] private readonly MobThresholdSystem     _thresholds = default!;
    [Dependency] private readonly SharedHandsSystem      _hands      = default!;
    [Dependency] private readonly IGameTiming            _timing     = default!;
    [Dependency] private readonly SharedAudioSystem      _audio      = default!;
    [Dependency] private readonly IPrototypeManager      _proto      = default!;

    private static readonly TimeSpan UpdateRate = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<N14PipboyComponent, BoundUIOpenedEvent>(OnUiOpened);

        Subs.BuiEvents<N14PipboyComponent>(N14PipboyUiKey.Key, subs =>
        {
            subs.Event<N14PipboyRequestUpdateMessage>(OnRequestUpdate);
            subs.Event<N14PipboyAddNoteMessage>(OnAddNote);
            subs.Event<N14PipboyDeleteNoteMessage>(OnDeleteNote);
            subs.Event<N14PipboyRadioSelectMessage>(OnRadioSelect);
            subs.Event<N14PipboyRadioPlayMessage>(OnRadioPlay);
            subs.Event<N14PipboyRadioStopMessage>(OnRadioStop);
            subs.Event<N14PipboyRadioSetTimeMessage>(OnRadioSetTime);
            subs.Event<N14PipboyPickupItemMessage>(OnPickupItem);
        });
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<N14PipboyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_ui.IsUiOpen(uid, N14PipboyUiKey.Key))
                continue;
            if (comp.NextUpdate > _timing.CurTime)
                continue;
            comp.NextUpdate = _timing.CurTime + UpdateRate;
            UpdateUi(uid, comp);
        }
    }

    // ── BUI Event handlers ──────────────────────────────────────────────

    private void OnUiOpened(EntityUid uid, N14PipboyComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, comp);
    }

    private void OnRequestUpdate(EntityUid uid, N14PipboyComponent comp,
        N14PipboyRequestUpdateMessage args)
    {
        UpdateUi(uid, comp);
    }

    private void OnAddNote(EntityUid uid, N14PipboyComponent comp,
        N14PipboyAddNoteMessage args)
    {
        if (string.IsNullOrWhiteSpace(args.Text))
            return;

        // Prepend author name
        var author = TryComp<ActorComponent>(args.Actor, out var actor)
            ? Name(args.Actor)
            : "Неизвестный";

        comp.Notes.Add($"[{author}] {args.Text.Trim()}");
        Dirty(uid, comp);
        UpdateUi(uid, comp);
    }

    private void OnDeleteNote(EntityUid uid, N14PipboyComponent comp,
        N14PipboyDeleteNoteMessage args)
    {
        if (args.Index >= 0 && args.Index < comp.Notes.Count)
            comp.Notes.RemoveAt(args.Index);
        Dirty(uid, comp);
        UpdateUi(uid, comp);
    }

    private void OnRadioSelect(EntityUid uid, N14PipboyComponent comp,
        N14PipboyRadioSelectMessage args)
    {
        comp.AudioStream    = _audio.Stop(comp.AudioStream);
        comp.SelectedSongId = args.SongId;
        Dirty(uid, comp);
    }

    private void OnRadioPlay(EntityUid uid, N14PipboyComponent comp,
        N14PipboyRadioPlayMessage args)
    {
        if (!args.Play)
        {
            _audio.SetState(comp.AudioStream, AudioState.Paused);
            Dirty(uid, comp);
            return;
        }

        if (Exists(comp.AudioStream))
        {
            _audio.SetState(comp.AudioStream, AudioState.Playing);
            Dirty(uid, comp);
            return;
        }

        if (string.IsNullOrEmpty(comp.SelectedSongId) ||
            !_proto.TryIndex<JukeboxPrototype>(comp.SelectedSongId, out var proto))
            return;

        comp.AudioStream = _audio.PlayPvs(
            proto.Path, uid, AudioParams.Default.WithMaxDistance(12f))?.Entity;
        Dirty(uid, comp);
    }

    private void OnRadioStop(EntityUid uid, N14PipboyComponent comp,
        N14PipboyRadioStopMessage args)
    {
        comp.AudioStream = _audio.Stop(comp.AudioStream);
        Dirty(uid, comp);
    }

    private void OnRadioSetTime(EntityUid uid, N14PipboyComponent comp,
        N14PipboyRadioSetTimeMessage args)
    {
        if (TryComp<ActorComponent>(args.Actor, out var actor))
        {
            var offset = actor.PlayerSession.Channel.Ping * 1.5f / 1000f;
            _audio.SetPlaybackPosition(comp.AudioStream, args.Time + offset);
        }
        else
        {
            _audio.SetPlaybackPosition(comp.AudioStream, args.Time);
        }
    }

    private void OnPickupItem(EntityUid uid, N14PipboyComponent comp,
        N14PipboyPickupItemMessage args)
    {
        // Resolve who is carrying the pipboy
        if (!_containers.TryGetContainingContainer((uid, null, null), out var container))
            return;

        var owner = container.Owner;
        var item  = GetEntity(args.ItemId);

        if (!EntityManager.EntityExists(item) || !EntityManager.EntityExists(owner))
            return;

        // Try to put the item into the owner's active hand
        _hands.TryPickup(owner, item);
    }

    // ── State builder ─────────────────────────────────────────────────────

    private void UpdateUi(EntityUid uid, N14PipboyComponent comp)
    {
        if (!_ui.HasUi(uid, N14PipboyUiKey.Key))
            return;

        // Find the entity wearing/holding the pipboy.
        EntityUid? owner = null;
        if (_containers.TryGetContainingContainer((uid, null, null), out var container))
            owner = container.Owner;

        var currentDamage = FixedPoint2.Zero;
        var maxHp         = FixedPoint2.New(100); // sensible default
        var capsCount     = 0;
        Dictionary<TargetBodyPart, TargetIntegrity>? bodyParts = null;
        NetEntity?                playerNetEntity = null;
        var items = new List<N14PipboyItemEntry>();

        if (owner.HasValue && !Deleted(owner.Value))
        {
            playerNetEntity = GetNetEntity(owner.Value);

            if (TryComp<DamageableComponent>(owner.Value, out var damageable))
                currentDamage = damageable.TotalDamage;

            // Use incap (Critical) threshold first; fall back to Dead
            if (_thresholds.TryGetIncapThreshold(owner.Value, out var incapThr) && incapThr.HasValue)
                maxHp = incapThr.Value;
            else if (_thresholds.TryGetThresholdForState(owner.Value, MobState.Dead, out var deadThr) && deadThr.HasValue)
                maxHp = deadThr.Value;

            if (HasComp<TargetingComponent>(owner.Value))
                bodyParts = _body.GetBodyPartStatus(owner.Value);

            // ── Items in hands ──────────────────────────────────────────
            if (TryComp<HandsComponent>(owner.Value, out var hands))
            {
                foreach (var hand in hands.Hands.Values)
                {
                    if (hand.HeldEntity is not { } heldUid) continue;
                    items.Add(new N14PipboyItemEntry(Name(heldUid), false, true, GetNetEntity(heldUid)));
                    CountCaps(heldUid, ref capsCount);
                }
            }

            // ── Items in inventory slots (equipment + bags) ─────────────
            var slotEnum = _inventory.GetSlotEnumerator(owner.Value);
            while (slotEnum.NextItem(out var slotItem))
            {
                items.Add(new N14PipboyItemEntry(Name(slotItem), true, false, GetNetEntity(slotItem)));
                CountCaps(slotItem, ref capsCount);

                // Recurse into storage containers (backpacks, bags, etc.)
                if (TryComp<StorageComponent>(slotItem, out var storage))
                {
                    foreach (var contained in storage.Container.ContainedEntities)
                    {
                        items.Add(new N14PipboyItemEntry(
                            Name(contained), false, false, GetNetEntity(contained), isInBag: true));
                        CountCaps(contained, ref capsCount);
                    }
                }
            }
        }

        var state = new N14PipboyUpdateState(
            currentDamage,
            maxHp,
            capsCount,
            bodyParts,
            playerNetEntity,
            items,
            new List<string>(comp.Notes));

        _ui.SetUiState(uid, N14PipboyUiKey.Key, state);
    }

    private void CountCaps(EntityUid item, ref int count)
    {
        var protoId = MetaData(item).EntityPrototype?.ID ?? string.Empty;
        if (!protoId.StartsWith("N14CurrencyCap"))
            return;
        count += TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;
    }
}
