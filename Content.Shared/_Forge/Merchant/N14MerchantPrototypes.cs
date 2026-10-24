using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Forge.Merchant;

/// <summary>
///     Список цен товаров: сопоставление прототипа сущности и цены в капсах.
///     Задаётся в YAML и переопределяет <c>StaticPrice</c> прототипа.
/// </summary>
[Prototype("n14MerchantPrice")]
public sealed partial class N14MerchantPricePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Цены: прототип сущности -> цена в капсах.</summary>
    [DataField("prices")]
    public Dictionary<EntProtoId, int> Prices { get; private set; } = new();
}

/// <summary>
///     Список иконок предпросмотра: сопоставление прототипа сущности и пути к RSI.
///     Для сущностей без записи используется <see cref="DefaultIcon"/>.
/// </summary>
[Prototype("n14MerchantIcon")]
public sealed partial class N14MerchantIconPrototype : IPrototype
{
    /// <summary>Дефолтная иконка для всех сущностей без явного пути.</summary>
    public static readonly ResPath DefaultIcon = new("/Textures/_NukeWake/icons/iconsother/musor.rsi");

    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Иконки: прототип сущности -> путь к RSI.</summary>
    [DataField("icons")]
    public Dictionary<EntProtoId, ResPath> Icons { get; private set; } = new();

    /// <summary>Путь к RSI для сущности (или <see cref="DefaultIcon"/>, если не задан).</summary>
    public ResPath GetIcon(EntProtoId proto)
        => Icons.TryGetValue(proto, out var path) ? path : DefaultIcon;
}
