using System.Linq;
using Content.Shared._N14.Cross;
using Content.Shared.Buckle.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Rotation;
using Content.Shared.Standing;

namespace Content.Server._N14.Cross;

public sealed partial class N14CrossServerSystem
{
    private void OnUncuffAttempt(ref UncuffAttemptEvent args)
    {
        if (args.Cancelled || !IsHungOnCrossWithCrossRestraints(args.Target))
            return;

        args.Cancelled = true;
        if (Exists(args.User))
            _popup.PopupClient(Loc.GetString("n14-cross-popup-cant-uncuff-while-hung"), args.User, args.User);
    }

    private void OnMobStateChanged(EntityUid uid, BuckleComponent component, MobStateChangedEvent args)
    {
        if (!IsBuckledToCross(component))
            return;

        RefreshMobStateVisual(uid, args.Component, component);
    }

    private void OnDowned(Entity<BuckleComponent> ent, ref DownedEvent args)
    {
        if (!IsBuckledToCross(ent.Comp))
            return;

        _appearance.SetData(ent.Owner, RotationVisuals.RotationState, RotationState.Vertical);
    }

    private bool IsHungOnCrossWithCrossRestraints(EntityUid target)
    {
        if (!TryComp<BuckleComponent>(target, out var buckle) || buckle.BuckledTo is not { } strap)
            return false;

        return HasComp<N14CrossComponent>(strap) && HasCrossRestraints(target);
    }

    private void ApplyRestraints(Entity<N14CrossComponent> cross, EntityUid target)
    {
        if (!TryComp<HandsComponent>(target, out _))
            return;

        var cuffable = EnsureComp<CuffableComponent>(target);
        if (HasCrossRestraints((target, cuffable)))
            return;

        var restraints = Spawn(cross.Comp.RestraintPrototype, Transform(target).Coordinates);
        if (!TryComp<HandcuffComponent>(restraints, out _) || !_cuffs.TryAddNewCuffs(target, target, restraints, cuffable))
            QueueDel(restraints);
    }

    private void RemoveRestraints(EntityUid target)
    {
        if (!TryComp<CuffableComponent>(target, out var cuffable))
            return;

        foreach (var cuffs in _cuffs.GetAllCuffs(cuffable).ToArray())
        {
            if (!HasComp<N14CrossRestraintComponent>(cuffs))
                continue;
            var cuffsBefore = cuffable.CuffedHandCount;
            _cuffs.Uncuff(target, null, cuffs, cuffable);

            // If Uncuff ignored these cuffs, remove them directly from the cuff container.
            if (cuffable.CuffedHandCount == cuffsBefore)
            {
                _container.Remove(cuffs, cuffable.Container, force: true);
                QueueDel(cuffs);
            }
        }
    }

    private void RefreshMobStateVisual(EntityUid target, MobStateComponent? mobState = null, BuckleComponent? buckle = null)
    {
        if (!Resolve(target, ref mobState, false))
            return;

        Resolve(target, ref buckle, false);

        var visualState = mobState.CurrentState;
        if (IsBuckledToCross(buckle) && visualState is MobState.Critical or MobState.SoftCritical)
            visualState = MobState.Alive;

        _appearance.SetData(target, MobStateVisuals.State, visualState);

        if (IsBuckledToCross(buckle))
            _appearance.SetData(target, RotationVisuals.RotationState, RotationState.Vertical);
    }

    private bool IsBuckledToCross(BuckleComponent? buckle)
    {
        return buckle?.BuckledTo is { } strap && HasComp<N14CrossComponent>(strap);
    }

    private bool HasCrossRestraints(EntityUid target)
    {
        return TryComp<CuffableComponent>(target, out var cuffable)
               && HasCrossRestraints((target, cuffable));
    }

    private bool HasCrossRestraints(Entity<CuffableComponent> ent)
    {
        return _cuffs.GetAllCuffs(ent.Comp).Any(cuffs => HasComp<N14CrossRestraintComponent>(cuffs));
    }
}
