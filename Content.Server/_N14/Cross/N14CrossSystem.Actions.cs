using Content.Shared._N14.Cross;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;

namespace Content.Server._N14.Cross;

public sealed partial class N14CrossServerSystem
{
    private void OnUnstrapAttempt(Entity<N14CrossComponent> cross, ref UnstrapAttemptEvent args)
    {
        Log.Info($"[N14Cross] UnstrapAttempt cross={ToPrettyString(cross.Owner)} target={ToPrettyString(args.Buckle.Owner)} user={(args.User is { } u ? ToPrettyString(u) : "<null>")} popup={args.Popup} busy={cross.Comp.Busy} allow={cross.Comp.AllowNextUnstrap} bypass={cross.Comp.BypassNextUnhangAttempt}");

        if (args.Cancelled)
            return;

        if (TerminatingOrDeleted(cross.Owner))
            return;

        if (cross.Comp.BypassNextUnhangAttempt)
        {
            cross.Comp.BypassNextUnhangAttempt = false;
            Log.Info($"[N14Cross] UnstrapAttempt bypassed once for cross={ToPrettyString(cross.Owner)}");
            return;
        }

        if (cross.Comp.AllowNextUnstrap)
        {
            Log.Info($"[N14Cross] UnstrapAttempt allowed by flag for cross={ToPrettyString(cross.Owner)}");
            return;
        }

        if (args.User is null)
        {
            args.Cancelled = true;
            Log.Warning($"[N14Cross] UnstrapAttempt cancelled: null user, cross={ToPrettyString(cross.Owner)}");
            return;
        }

        if (args.User is not { } user)
            return;

        if (!args.Popup)
        {
            args.Cancelled = true;
            Log.Info($"[N14Cross] UnstrapAttempt cancelled: popup=false cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)}");
            return;
        }

        if (args.Buckle.Owner == user)
        {
            args.Cancelled = true;
            _popup.PopupEntity(Loc.GetString("n14-cross-popup-self-unhang-denied"), cross.Owner, user);
            Log.Info($"[N14Cross] UnstrapAttempt denied self-unhang cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)}");
            return;
        }

        args.Cancelled = true;

        if (!TryBeginAction(cross, user, popup: true))
            return;

        if (!TryStartUnhangDoAfter(cross, user, args.Buckle.Owner))
        {
            Log.Warning($"[N14Cross] Failed to start unhang DoAfter cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} target={ToPrettyString(args.Buckle.Owner)}");
            return;
        }

        _popup.PopupEntity(Loc.GetString("n14-cross-popup-unhang-start", ("target", args.Buckle.Owner)), cross.Owner, user);
        Log.Info($"[N14Cross] Unhang DoAfter started cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} target={ToPrettyString(args.Buckle.Owner)} busy={cross.Comp.Busy}");
    }

    private bool TryBeginAction(Entity<N14CrossComponent> cross, EntityUid user, bool popup)
    {
        if (!cross.Comp.Busy)
            return true;

        if (cross.Comp.BusyUntil is not { } busyUntil || _timing.CurTime > busyUntil)
        {
            Log.Warning($"[N14Cross] Stale busy reset cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} busyUntil={(cross.Comp.BusyUntil?.ToString() ?? "<null>")}");
            cross.Comp.Busy = false;
            cross.Comp.BusyUntil = null;
            return true;
        }

        if (popup)
            _popup.PopupEntity(Loc.GetString("n14-cross-popup-busy"), cross.Owner, user);

        Log.Warning($"[N14Cross] Action blocked by busy cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} busyUntil={busyUntil} allow={cross.Comp.AllowNextUnstrap} bypassHang={cross.Comp.BypassNextHangAttempt} bypassUnhang={cross.Comp.BypassNextUnhangAttempt}");
        return false;
    }

    private void OnHangDoAfter(Entity<N14CrossComponent> cross, ref N14CrossHangDoAfterEvent args)
    {
        cross.Comp.Busy = false;
        cross.Comp.BusyUntil = null;

        var user = args.Args.User;

        if (args.Handled || args.Cancelled)
        {
            Log.Info($"[N14Cross] Hang DoAfter finished early cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} cancelled={args.Cancelled} handled={args.Handled}");
            if (args.Cancelled && Exists(user))
                _popup.PopupEntity(Loc.GetString("n14-cross-popup-hang-interrupted"), cross.Owner, user);
            return;
        }

        var target = GetEntity(args.HangTarget);

        if (!Exists(user) || !Exists(target) || TerminatingOrDeleted(target))
        {
            Log.Warning($"[N14Cross] Hang DoAfter invalid entities cross={ToPrettyString(cross.Owner)} userExists={Exists(user)} targetExists={Exists(target)} targetDeleted={TerminatingOrDeleted(target)}");
            return;
        }

        if (!TryCompleteHang(cross, user, target))
        {
            _popup.PopupEntity(Loc.GetString("n14-cross-popup-hang-fail", ("target", target)), cross.Owner, user);
            Log.Warning($"[N14Cross] Hang completion failed cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} target={ToPrettyString(target)}");
            return;
        }

        _popup.PopupEntity(Loc.GetString("n14-cross-popup-hang-success-user", ("target", target)), cross.Owner, user);
        if (target != user)
            _popup.PopupEntity(Loc.GetString("n14-cross-popup-hang-success-target", ("user", user)), cross.Owner, target);

        Log.Info($"[N14Cross] Hang completed cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} target={ToPrettyString(target)}");
        args.Handled = true;
    }

    private bool TryCompleteHang(Entity<N14CrossComponent> cross, EntityUid user, EntityUid target)
    {
        cross.Comp.BypassNextHangAttempt = true;
        var success = _buckle.TryBuckle(target, user, cross.Owner, popup: true);
        if (cross.Comp.BypassNextHangAttempt)
            cross.Comp.BypassNextHangAttempt = false;

        return success;
    }

    private void OnUnhangDoAfter(Entity<N14CrossComponent> cross, ref N14CrossUnhangDoAfterEvent args)
    {
        cross.Comp.Busy = false;
        cross.Comp.BusyUntil = null;

        if (args.Handled || args.Cancelled)
        {
            Log.Info($"[N14Cross] Unhang DoAfter finished early cross={ToPrettyString(cross.Owner)} user={ToPrettyString(args.Args.User)} cancelled={args.Cancelled} handled={args.Handled}");
            if (args.Cancelled && Exists(args.Args.User))
                _popup.PopupEntity(Loc.GetString("n14-cross-popup-unhang-interrupted"), cross.Owner, args.Args.User);
            return;
        }

        var user = args.Args.User;
        var target = GetEntity(args.UnhangTarget);

        if (!Exists(user) || !Exists(target) || TerminatingOrDeleted(target))
        {
            Log.Warning($"[N14Cross] Unhang DoAfter invalid entities cross={ToPrettyString(cross.Owner)} userExists={Exists(user)} targetExists={Exists(target)} targetDeleted={TerminatingOrDeleted(target)}");
            return;
        }

        if (!TryCompleteUnhang(cross, user, target))
        {
            _popup.PopupEntity(Loc.GetString("n14-cross-popup-unhang-fail", ("target", target)), cross.Owner, user);
            Log.Warning($"[N14Cross] Unhang completion failed cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} target={ToPrettyString(target)} allow={cross.Comp.AllowNextUnstrap}");
            return;
        }

        _popup.PopupEntity(Loc.GetString("n14-cross-popup-unhang-success", ("target", target)), cross.Owner, user);
        Log.Info($"[N14Cross] Unhang completed cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} target={ToPrettyString(target)}");
        args.Handled = true;
    }

    private bool TryCompleteUnhang(Entity<N14CrossComponent> cross, EntityUid user, EntityUid target)
    {
        if (!TryComp<BuckleComponent>(target, out var buckle) || buckle.BuckledTo != cross.Owner)
        {
            var buckledTo = TryComp<BuckleComponent>(target, out var b) && b.BuckledTo is { } buckledToUid
                ? ToPrettyString(buckledToUid)
                : "<none>";
            Log.Warning($"[N14Cross] TryCompleteUnhang rejected cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} target={ToPrettyString(target)} buckleComp={HasComp<BuckleComponent>(target)} buckledTo={buckledTo}");
            return false;
        }

        cross.Comp.AllowNextUnstrap = true;
        _buckle.Unbuckle((target, buckle), user);
        Log.Info($"[N14Cross] Forced Unbuckle executed cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} target={ToPrettyString(target)}");
        return true;
    }

    private bool TryStartUnhangDoAfter(Entity<N14CrossComponent> cross, EntityUid user, EntityUid target)
    {
        var ev = new N14CrossUnhangDoAfterEvent { UnhangTarget = GetNetEntity(target), };
        return TryStartCrossDoAfter(cross, user, cross.Comp.UnhangDelay, ev);
    }

    private bool TryStartCrossDoAfter(
        Entity<N14CrossComponent> cross,
        EntityUid user,
        TimeSpan delay,
        DoAfterEvent doAfterEvent
    )
    {
        var doAfter = new DoAfterArgs(EntityManager, user, delay, doAfterEvent, cross.Owner, target: cross.Owner)
        {
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = SharedInteractionSystem.InteractionRange
        };

        cross.Comp.Busy = true;
        cross.Comp.BusyUntil = _timing.CurTime + delay + TimeSpan.FromSeconds(1);

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            cross.Comp.Busy = false;
            cross.Comp.BusyUntil = null;
            Log.Warning($"[N14Cross] TryStartDoAfter failed cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} delay={delay}");
            return false;
        }

        Log.Info($"[N14Cross] Busy=true cross={ToPrettyString(cross.Owner)} user={ToPrettyString(user)} doAfter={doAfterEvent.GetType().Name} busyUntil={cross.Comp.BusyUntil}");
        return true;
    }
}



