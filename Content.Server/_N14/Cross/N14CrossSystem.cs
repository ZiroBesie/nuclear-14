using Content.Shared._N14.Cross;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._N14.Cross;

public sealed partial class N14CrossServerSystem : EntitySystem
{
    private const string OccupiedOverlayPrototype = "N14CrossOccupiedOverlay";

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffs = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly Dictionary<EntityUid, EntityUid> _occupiedOverlays = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeCrossEvents();
        SubscribeRestraintEvents();
    }

    private void SubscribeCrossEvents()
    {
        SubscribeLocalEvent<N14CrossComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<N14CrossComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<N14CrossComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<N14CrossComponent, N14CrossHangDoAfterEvent>(OnHangDoAfter);
        SubscribeLocalEvent<N14CrossComponent, N14CrossUnhangDoAfterEvent>(OnUnhangDoAfter);
        SubscribeLocalEvent<N14CrossComponent, BreakageEventArgs>(OnCrossBroken);
        SubscribeLocalEvent<N14CrossComponent, DestructionEventArgs>(OnCrossDestroyed);
        SubscribeLocalEvent<N14CrossComponent, MapInitEvent>(OnCrossMapInit);
        SubscribeLocalEvent<N14CrossComponent, MoveEvent>(OnCrossMove);
        SubscribeLocalEvent<N14CrossComponent, ComponentShutdown>(OnCrossShutdown);
    }

    private void SubscribeRestraintEvents()
    {
        SubscribeLocalEvent<UncuffAttemptEvent>(OnUncuffAttempt);
        SubscribeLocalEvent<BuckleComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BuckleComponent, DownedEvent>(OnDowned);
    }
}

