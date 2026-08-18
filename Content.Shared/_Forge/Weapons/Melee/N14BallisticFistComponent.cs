using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Forge.Weapons.Melee;

/// <summary>
///     Отвечает за баллистический кулак: при ударе кулаком (MeleeHit) оружие
///     производит выстрел из встроенного ствола и затем уходит на перезарядку.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class N14BallisticFistComponent : Component
{
    /// <summary>
    ///     Время (в секундах) между выстрелами. По сути — время ухода на перезарядку,
    ///     чтобы нельзя было стрелять каждый удар без ограничений.
    /// </summary>
    [DataField]
    public float FireCooldown = 1f;

    /// <summary>
    ///     Дистанция, на которую направлен выстрел от точки удара.
    /// </summary>
    [DataField]
    public float Range = 3f;

    /// <summary>
    ///     Момент времени, когда кулак снова сможет выстрелить.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextFireTime;
}