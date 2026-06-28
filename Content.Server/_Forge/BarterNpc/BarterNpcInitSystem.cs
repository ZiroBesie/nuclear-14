using Content.Server.Carrying;
using Content.Shared._N14.BarterNpc;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Strip.Components;
using Robust.Shared.GameObjects;

namespace Content.Server._N14.BarterNpc;

/// <summary>
/// Убирает нежелательные компоненты у бартерного НПС при инициализации.
/// </summary>
public sealed class BarterNpcInitSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BarterNpcComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, BarterNpcComponent comp, ComponentStartup args)
    {
        // Нельзя обыскать / раздеть
        RemCompDeferred<StrippableComponent>(uid);

        // Нельзя тащить
        RemCompDeferred<PullableComponent>(uid);

        // Нельзя поднять (переносить на руках)
        RemCompDeferred<CarriableComponent>(uid);
    }
}
