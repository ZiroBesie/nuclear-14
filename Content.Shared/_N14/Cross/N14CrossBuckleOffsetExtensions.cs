using System.Numerics;
using Content.Shared.Buckle.Components;

namespace Content.Shared.Buckle;

public abstract partial class SharedBuckleSystem
{
    public bool N14TrySetBuckleOffset(EntityUid strapUid, Vector2 offset, StrapComponent? strap = null)
    {
        if (!Resolve(strapUid, ref strap, false))
            return false;

        if ((strap.BuckleOffset - offset).LengthSquared() <= 1e-6f)
            return false;

        strap.BuckleOffset = offset;
        Dirty(strapUid, strap);
        return true;
    }
}
