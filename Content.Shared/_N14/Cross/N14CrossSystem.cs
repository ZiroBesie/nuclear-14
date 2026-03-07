using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Pulling.Events;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._N14.Cross;

public sealed class N14CrossSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<N14CrossComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<MobStateComponent, BeingPulledAttemptEvent>(OnBeingPulledAttempt);
    }

    private void OnBeingPulledAttempt(Entity<MobStateComponent> ent, ref BeingPulledAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<BuckleComponent>(ent.Owner, out var buckle) || buckle.BuckledTo is not { } strap)
            return;

        if (!HasComp<N14CrossComponent>(strap))
            return;

        args.Cancel();
    }

    private void OnStrapAttempt(Entity<N14CrossComponent> cross, ref StrapAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (cross.Comp.BypassNextHangAttempt)
        {
            cross.Comp.BypassNextHangAttempt = false;
            return;
        }

        args.Cancelled = true;

        if (!_net.IsServer)
            return;

        if (args.User is not { } user)
            return;

        if (cross.Comp.Busy)
        {
            if (cross.Comp.BusyUntil is not { } busyUntil || _timing.CurTime > busyUntil)
            {
                cross.Comp.Busy = false;
                cross.Comp.BusyUntil = null;
            }
            else
            {
                if (args.Popup)
                    _popup.PopupEntity(Loc.GetString("n14-cross-popup-busy"), cross.Owner, user);
                return;
            }
        }

        var target = args.Buckle.Owner;
        var ev = new N14CrossHangDoAfterEvent { HangTarget = GetNetEntity(target) };
        var doAfter = new DoAfterArgs(EntityManager, user, cross.Comp.HangDelay, ev, cross.Owner, target: cross.Owner)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = SharedInteractionSystem.InteractionRange
        };

        cross.Comp.Busy = true;
        cross.Comp.BusyUntil = _timing.CurTime + cross.Comp.HangDelay + TimeSpan.FromSeconds(1);

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            cross.Comp.Busy = false;
            cross.Comp.BusyUntil = null;
            return;
        }

        _popup.PopupEntity(Loc.GetString("n14-cross-popup-hang-start", ("target", target)), cross.Owner, user);
    }
}


