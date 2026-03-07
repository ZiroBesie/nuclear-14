using System.Linq;
using System.Numerics;
using Content.Shared._N14.Cross;
using Content.Shared.Buckle.Components;
using Content.Shared.Destructible;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server._N14.Cross;

public sealed partial class N14CrossServerSystem
{
    private void OnStrapped(Entity<N14CrossComponent> cross, ref StrappedEvent args)
    {
        cross.Comp.Busy = false;
        cross.Comp.BusyUntil = null;
        Log.Info($"[N14Cross] Strapped cross={ToPrettyString(cross.Owner)} target={ToPrettyString(args.Buckle.Owner)} busyReset=true");

        SyncDirectionalBuckleOffset(cross);
        ApplyRestraints(cross, args.Buckle.Owner);
        _pulling.TryStopPull(args.Buckle.Owner);
        RefreshMobStateVisual(args.Buckle.Owner, buckle: args.Buckle.Comp);
        UpdateOccupiedOverlay(cross);
    }

    private void OnUnstrapped(Entity<N14CrossComponent> cross, ref UnstrappedEvent args)
    {
        cross.Comp.Busy = false;
        cross.Comp.BusyUntil = null;
        var target = args.Buckle.Owner;
        Log.Info($"[N14Cross] Unstrapped cross={ToPrettyString(cross.Owner)} target={ToPrettyString(target)} allowFlagBefore={cross.Comp.AllowNextUnstrap}");

        if (!CanRemoveRestraintsOnUnstrap(cross))
        {
            // Unwanted unbuckle (e.g. physics impulse from damage bypassing UnstrapAttemptEvent).
            // Re-buckle the entity immediately.
            cross.Comp.BypassNextHangAttempt = true;
            var success = _buckle.TryBuckle(target, null, cross.Owner, popup: false);
            if (cross.Comp.BypassNextHangAttempt)
                cross.Comp.BypassNextHangAttempt = false;

            Log.Warning($"[N14Cross] Unstrapped without AllowNextUnstrap, rebuckle attempt cross={ToPrettyString(cross.Owner)} target={ToPrettyString(target)} success={success}");

            if (!success)
                RemoveRestraints(target);

            RefreshMobStateVisual(target);
            UpdateOccupiedOverlay(cross);
            return;
        }

        RemoveRestraints(target);
        RefreshMobStateVisual(target);
        UpdateOccupiedOverlay(cross);
        Log.Info($"[N14Cross] Unstrap cleanup completed cross={ToPrettyString(cross.Owner)} target={ToPrettyString(target)}");
    }

    private bool CanRemoveRestraintsOnUnstrap(Entity<N14CrossComponent> cross)
    {
        var allowRemoval = TerminatingOrDeleted(cross.Owner) || cross.Comp.AllowNextUnstrap;
        cross.Comp.AllowNextUnstrap = false;
        Log.Info($"[N14Cross] CanRemoveRestraintsOnUnstrap cross={ToPrettyString(cross.Owner)} result={allowRemoval}");
        return allowRemoval;
    }

    private void OnCrossMapInit(Entity<N14CrossComponent> cross, ref MapInitEvent args)
    {
        cross.Comp.Busy = false;
        cross.Comp.BusyUntil = null;
        SyncDirectionalBuckleOffset(cross);
        UpdateOccupiedOverlay(cross);
    }

    private void OnCrossMove(Entity<N14CrossComponent> cross, ref MoveEvent args)
    {
        SyncDirectionalBuckleOffset(cross);
    }

    private void SyncDirectionalBuckleOffset(Entity<N14CrossComponent> cross)
    {
        if (!TryComp<StrapComponent>(cross.Owner, out var strap))
            return;

        var direction = Transform(cross.Owner).WorldRotation.GetCardinalDir();
        var offset = cross.Comp.GetBuckleOffset(direction, strap.BuckleOffset);
        _buckle.N14TrySetBuckleOffset(cross.Owner, offset, strap);

        foreach (var buckled in strap.BuckledEntities)
        {
            var buckleXform = Transform(buckled);
            if (buckleXform.ParentUid != cross.Owner)
                continue;

            var coords = new EntityCoordinates(cross.Owner, offset);
            _transform.SetCoordinates(buckled, buckleXform, coords, rotation: Angle.Zero);
        }
    }

    private void UpdateOccupiedOverlay(Entity<N14CrossComponent> cross)
    {
        if (!TryComp<StrapComponent>(cross.Owner, out var strap))
        {
            RemoveOccupiedOverlay(cross.Owner);
            return;
        }

        if (strap.BuckledEntities.Count == 0)
        {
            RemoveOccupiedOverlay(cross.Owner);
            return;
        }

        EnsureOccupiedOverlay(cross.Owner);
    }

    private void EnsureOccupiedOverlay(EntityUid crossUid)
    {
        if (_occupiedOverlays.TryGetValue(crossUid, out var existing))
        {
            if (!TerminatingOrDeleted(existing))
                return;

            _occupiedOverlays.Remove(crossUid);
        }

        var overlay = Spawn(OccupiedOverlayPrototype, new EntityCoordinates(crossUid, Vector2.Zero));
        _occupiedOverlays[crossUid] = overlay;
    }

    private void RemoveOccupiedOverlay(EntityUid crossUid)
    {
        if (!_occupiedOverlays.TryGetValue(crossUid, out var overlay))
            return;

        _occupiedOverlays.Remove(crossUid);
        if (!TerminatingOrDeleted(overlay))
            QueueDel(overlay);
    }

    private void OnCrossShutdown(Entity<N14CrossComponent> cross, ref ComponentShutdown args)
    {
        PrepareCrossForRemoval(cross);
    }

    private void OnCrossBroken(Entity<N14CrossComponent> cross, ref BreakageEventArgs args)
    {
        PrepareCrossForRemoval(cross);
    }

    private void OnCrossDestroyed(Entity<N14CrossComponent> cross, ref DestructionEventArgs args)
    {
        PrepareCrossForRemoval(cross);
    }

    private void PrepareCrossForRemoval(Entity<N14CrossComponent> cross)
    {
        RemoveOccupiedOverlay(cross.Owner);

        if (!TryComp<StrapComponent>(cross.Owner, out var strap))
            return;

        foreach (var buckled in strap.BuckledEntities.ToArray())
        {
            if (TerminatingOrDeleted(buckled))
                continue;

            if (TryComp<BuckleComponent>(buckled, out var buckle) && buckle.BuckledTo == cross.Owner)
            {
                cross.Comp.AllowNextUnstrap = true;
                _buckle.Unbuckle((buckled, buckle), null);
            }

            RemoveRestraints(buckled);
            RefreshMobStateVisual(buckled);
        }

        cross.Comp.AllowNextUnstrap = false;
    }
}

