using System.Numerics;
using Content.Shared._Forge.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Forge.Weapons.Melee;

/// <summary>
///     Обрабатывает стрельбу баллистического кулака: когда наносится удачный удар кулаком,
///     оружие производит один выстрел из встроенного магазина и затем уходит на перезарядку.
/// </summary>
public sealed class N14BallisticFistSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<N14BallisticFistComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<N14BallisticFistComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        var (uid, comp) = ent;

        if (!TryComp<GunComponent>(uid, out var gun))
            return;

        // Кулак уходит на перезарядку: пока не прошёл кулдаун — выстрел невозможен.
        if (_timing.CurTime < comp.NextFireTime)
            return;

        // Проверяем наличие патронов во встроенном магазине.
        var countEv = new GetAmmoCountEvent();
        RaiseLocalEvent(uid, ref countEv);
        if (countEv.Count <= 0)
            return;

        // Определяем направление выстрела: в сторону удара либо в сторону цели.
        Vector2 direction;
        if (args.Direction is { } dir && dir != Vector2.Zero)
        {
            direction = dir.Normalized();
        }
        else if (args.HitEntities.Count > 0)
        {
            var hitPos = _transform.GetMapCoordinates(args.HitEntities[0]).Position;
            var userPos = _transform.GetMapCoordinates(args.User).Position;
            var delta = hitPos - userPos;
            if (delta == Vector2.Zero)
                return;
            direction = delta.Normalized();
        }
        else
        {
            return;
        }

        // Забираем один патрон из магазина.
        var fromCoordinates = Transform(args.User).Coordinates;
        var toCoordinates = fromCoordinates.Offset(direction * comp.Range);
        var ammo = new List<(EntityUid? Entity, IShootable Shootable)>();
        var takeAmmo = new TakeAmmoEvent(1, ammo, fromCoordinates, args.User);
        RaiseLocalEvent(uid, takeAmmo);

        if (ammo.Count == 0)
            return;

        _gun.Shoot(uid, gun, ammo, fromCoordinates, toCoordinates, out _, args.User);

        // Уходим на перезарядку: ставим кулдаун перед следующим возможным выстрелом.
        comp.NextFireTime = _timing.CurTime + TimeSpan.FromSeconds(comp.FireCooldown);
        Dirty(ent);
    }
}