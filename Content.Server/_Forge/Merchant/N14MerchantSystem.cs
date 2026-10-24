using System.Diagnostics.CodeAnalysis;
using Content.Shared._Forge.Merchant;
using Content.Shared._Forge.Weapons.Ranged.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Server.Cargo.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Forge.Merchant;

/// <summary>
///     Серверная логика торговца: отправка состояния, покупка за капсы, продажа за капсы.
/// </summary>
public sealed partial class N14MerchantSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private const string CapsStackId = "Caps";

    /// <summary>Тег внутреннего магазина/спидлоадера: считается частью оружия, не отдельной вещью.</summary>
    private static readonly ProtoId<TagPrototype> SpeedLoaderInternalTag = "SpeedLoaderInternal";

    /// <summary>Наценка торговца на купленные у игрока вещи (25%).</summary>
    private const double MerchantMarkup = 1.25;

    /// <summary>Прототипы-списки цен и иконок (задаются в YAML).</summary>
    private static readonly ProtoId<N14MerchantPricePrototype> PriceListId = "N14MerchantPrices";
    private static readonly ProtoId<N14MerchantIconPrototype> IconListId = "N14MerchantIcons";

    private static readonly SoundSpecifier BuySound = new SoundPathSpecifier("/Audio/Effects/kaching.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<N14MerchantComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<N14MerchantComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<N14MerchantComponent, N14MerchantCommitMessage>(OnCommit);
    }

    private void OnUiOpened(EntityUid uid, N14MerchantComponent comp, BoundUIOpenedEvent args)
    {
        // Пока окно открыто — игрок не может ходить.
        EnsureComp<N14MerchantTradeUserComponent>(args.Actor);
        _ui.SetUiState(uid, N14MerchantUiKey.Key, BuildState(uid, comp, args.Actor));
    }

    private void OnUiClosed(EntityUid uid, N14MerchantComponent comp, BoundUIClosedEvent args)
    {
        RemComp<N14MerchantTradeUserComponent>(args.Actor);
    }

    private void OnCommit(EntityUid uid, N14MerchantComponent comp, N14MerchantCommitMessage args)
    {
        var buyer = args.Actor;
        var changed = false;

        foreach (var index in args.BuyIndices)
        {
            if (index < 0 || index >= comp.Stock.Count)
                continue;

            var listing = comp.Stock[index];
            if (listing.Remaining == 0)
                continue;

            var price = GetPrice(listing.Product.Id, listing.Price);
            if (price > 0 && !TryTakeCaps(buyer, price))
                continue;

            comp.Caps += price;
            listing.Remaining = listing.Remaining < 0 ? -1 : listing.Remaining - 1;
            comp.Stock[index] = listing;

            var product = Spawn(listing.Product, Transform(buyer).Coordinates);
            _hands.PickupOrDrop(buyer, product);
            changed = true;
        }

        foreach (var proto in args.SellProtos)
        {
            if (!TryGetSellPrice(comp, proto, out var price))
                continue;

            if (price > comp.Caps)
                continue;

            if (!TryConsumeItem(buyer, proto))
                continue;

            comp.Caps -= price;
            GiveCaps(buyer, price);

            // Купленная вещь уходит в ассортимент торговца с наценкой.
            AddToStock(comp, proto, price);
            changed = true;
        }

        if (changed)
            _audio.PlayEntity(BuySound, buyer, uid);

        if (_ui.IsUiOpen(uid, N14MerchantUiKey.Key))
            _ui.SetUiState(uid, N14MerchantUiKey.Key, BuildState(uid, comp, buyer));
    }

    private bool TryFindWant(N14MerchantComponent comp, string proto, out N14MerchantListing want)
    {
        foreach (var listing in comp.Wants)
        {
            if (listing.Product.Id == proto)
            {
                want = listing;
                return true;
            }
        }

        want = default;
        return false;
    }

    private N14MerchantUiState BuildState(EntityUid uid, N14MerchantComponent comp, EntityUid actor)
    {
        var state = new N14MerchantUiState
        {
            MerchantName = Identity.Name(uid, EntityManager),
            MerchantCaps = comp.Caps,
            PlayerCaps = CountCaps(actor),
        };

        for (var i = 0; i < comp.Stock.Count; i++)
        {
            var listing = comp.Stock[i];

            if (listing.Remaining == 0)
                continue;

            if (!_prototype.TryIndex<EntityPrototype>(listing.Product, out var proto))
                continue;

            var item = new N14MerchantItemState
            {
                Id = "o" + i,
                Index = i,
                Proto = listing.Product.Id,
                Name = proto.Name,
                Description = proto.Description,
                Price = GetPrice(listing.Product.Id, listing.Price),
                Count = listing.Remaining,
            };

            // Товар торговца не заспавнен — читаем данные из прототипа (у нового товара прочность полная).
            FillWeaponInfo(item, proto, null, actor);
            state.Offers.Add(item);
        }

        BuildRequests(comp, actor, state);
        return state;
    }

    /// <summary>
    ///     Список вещей в руках и экипировке игрока (с заходом в контейнеры вроде рюкзака).
    /// </summary>
    private void BuildRequests(N14MerchantComponent comp, EntityUid actor, N14MerchantUiState state)
    {
        var prices = new Dictionary<string, int>();
        foreach (var want in comp.Wants)
        {
            if (!prices.ContainsKey(want.Product.Id))
                prices[want.Product.Id] = want.Price;
        }

        var owned = new Dictionary<string, int>();
        var order = new List<string>();
        var samples = new Dictionary<string, EntityUid>();

        // Сущности, вставленные в магазин оружия (спидлоадеры/магазины) — часть оружия, не отдельные вещи.
        var insertedMags = CollectInsertedMagazines(actor);

        foreach (var ent in EnumerateInventory(actor))
        {
            if (!TryGetProtoId(ent, out var id))
                continue;

            // Капсы — это валюта, в списке предметов их не показываем.
            if (TryComp<StackComponent>(ent, out var stack) && stack.StackTypeId == CapsStackId)
                continue;

            // Патроны и магазины в продаже не показываем (id содержит Cartridge/Magazine).
            if (IsAmmoOrMagazine(id))
                continue;

            // Внутренний магазин/спидлоадер считается частью оружия, а не отдельной вещью.
            if (_tag.HasTag(ent, SpeedLoaderInternalTag) || insertedMags.Contains(ent))
                continue;

            if (owned.TryAdd(id, 1))
            {
                order.Add(id);
                samples[id] = ent;
            }
            else
            {
                owned[id]++;
            }
        }

        for (var i = 0; i < order.Count; i++)
        {
            var id = order[i];

            if (!_prototype.TryIndex<EntityPrototype>(id, out var proto))
                continue;

            var item = new N14MerchantItemState
            {
                Id = "r" + i,
                Proto = id,
                Name = proto.Name,
                Description = proto.Description,
                Price = GetPrice(id, prices.GetValueOrDefault(id)),
                Count = owned[id],
            };

            // Вещи игрока заспавнены — читаем реальную прочность и патроны.
            samples.TryGetValue(id, out var sample);
            FillWeaponInfo(item, proto, sample, actor);
            state.Requests.Add(item);
        }
    }

    private bool TryGetProtoId(EntityUid uid, [NotNullWhen(true)] out string? protoId)
    {
        protoId = MetaData(uid).EntityPrototype?.ID;
        return protoId != null;
    }

    /// <summary>
    ///     Цена вещи: цена из YAML-списка цен -> явная цена из конфига -> StaticPrice у прототипа.
    /// </summary>
    private int GetPrice(string protoId, int configured)
    {
        // 1. Список цен из YAML (наивысший приоритет).
        if (_prototype.TryIndex(PriceListId, out var priceList) &&
            priceList.Prices.TryGetValue(protoId, out var listed))
        {
            return listed;
        }

        // 2. Явная цена из конфига торговца.
        if (configured > 0)
            return configured;

        // 3. StaticPrice у прототипа.
        if (_prototype.TryIndex<EntityPrototype>(protoId, out var proto) &&
            proto.TryGetComponent<StaticPriceComponent>(out var staticPrice))
        {
            return (int)Math.Round(staticPrice.Price);
        }

        return 0;
    }

    /// <summary>Путь к RSI-иконке предпросмотра для прототипа (или дефолт).</summary>
    private ResPath GetIconPath(string protoId)
    {
        if (_prototype.TryIndex(IconListId, out var iconList))
            return iconList.GetIcon(protoId);

        return N14MerchantIconPrototype.DefaultIcon;
    }

    /// <summary>
    ///     Заполняет данные об оружии для строки UI: прочность, калибр, патроны в магазине и у игрока.
    /// </summary>
    /// <param name="entity">Заспавненная сущность (для вещей игрока) или null (товар торговца из прототипа).</param>
    private void FillWeaponInfo(N14MerchantItemState item, EntityPrototype proto, EntityUid? entity, EntityUid actor)
    {
        // Иконка предпросмотра из YAML-списка (или дефолт).
        item.IconPath = GetIconPath(proto.ID).ToString();

        // Прочность.
        if (entity != null && TryComp<GunConditionComponent>(entity, out var cond) && cond.MaxCondition > 0)
        {
            item.Condition = Math.Clamp(cond.Condition / cond.MaxCondition, 0f, 1f);
        }
        else if (proto.TryGetComponent<GunConditionComponent>(out var protoCond) && protoCond.MaxCondition > 0)
        {
            item.Condition = Math.Clamp(protoCond.Condition / protoCond.MaxCondition, 0f, 1f);
        }

        // Калибр и магазин.
        BallisticAmmoProviderComponent? ballistic = null;

        if (entity != null)
            TryComp(entity, out ballistic);
        else
            proto.TryGetComponent(out ballistic);

        if (ballistic == null)
            return;

        item.MagCapacity = ballistic.Capacity;
        item.AmmoInMag = entity != null
            ? Math.Clamp(ballistic.Count, 0, ballistic.Capacity)
            : ballistic.Capacity; // новый товар при спавне полон

        var tags = GetCaliberTags(ballistic);

        // Если у самого оружия калиберных тегов нет (оно работает только через вставленный
        // магазин), берём калибр из вставленного внутреннего магазина/спидлоадера.
        if ((tags == null || !HasCaliberTag(tags)) && entity != null)
            tags = GetInsertedMagazineCaliberTags(entity.Value) ?? tags;

        if (tags == null || tags.Count == 0)
            return;

        item.Caliber = FormatCaliber(tags[0].Id);
        item.AmmoInBag = CountAmmoInInventory(actor, tags, entity);
    }

    /// <summary>Есть ли среди переданных тегов хотя бы один калиберный (N14Cartridge*/N14Shell*).</summary>
    private static bool HasCaliberTag(List<ProtoId<TagPrototype>> tags)
    {
        foreach (var tag in tags)
        {
            if (IsCaliberTag(tag.Id))
                return true;
        }

        return false;
    }

    private static bool IsCaliberTag(string id)
        => id.StartsWith("N14Cartridge", StringComparison.Ordinal) ||
           id.StartsWith("N14Shell", StringComparison.Ordinal);

    /// <summary>Патрон или магазин по id прототипа (содержит Cartridge/Magazine) — не продаётся.</summary>
    private static bool IsAmmoOrMagazine(string protoId)
        => protoId.Contains("Cartridge", StringComparison.OrdinalIgnoreCase) ||
           protoId.Contains("Magazine", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Калиберные теги вставленного в оружие внутреннего магазина/спидлоадера (если есть).
    /// </summary>
    private List<ProtoId<TagPrototype>>? GetInsertedMagazineCaliberTags(EntityUid weapon)
    {
        if (!TryComp<ContainerManagerComponent>(weapon, out var manager))
            return null;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var ent in container.ContainedEntities)
            {
                if (!TryComp<BallisticAmmoProviderComponent>(ent, out var mag))
                    continue;

                var tags = GetCaliberTags(mag);
                if (tags != null && HasCaliberTag(tags))
                    return tags;
            }
        }

        return null;
    }

    /// <summary>
    ///     Теги калибра из whitelist патронов оружия (теги вида N14Cartridge*/N14Shell*).
    /// </summary>
    private static List<ProtoId<TagPrototype>>? GetCaliberTags(BallisticAmmoProviderComponent ballistic)
    {
        var all = ballistic.Whitelist?.Tags;
        if (all == null || all.Count == 0)
            return null;

        var result = new List<ProtoId<TagPrototype>>();

        foreach (var tag in all)
        {
            if (IsCaliberTag(tag.Id))
                result.Add(tag);
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    ///     Человекочитаемый калибр: "N14Cartridge45-70" -> ".45-70", "N14ShellShotgun12" -> "12ga".
    /// </summary>
    private static string FormatCaliber(string tagId)
    {
        if (tagId.StartsWith("N14Cartridge", StringComparison.Ordinal))
        {
            var rest = tagId["N14Cartridge".Length..];
            return rest.Length > 0 && char.IsDigit(rest[0]) ? "." + rest : rest;
        }

        if (tagId.StartsWith("N14ShellShotgun", StringComparison.Ordinal))
        {
            var rest = tagId["N14ShellShotgun".Length..];
            return rest + "ga";
        }

        if (tagId.StartsWith("N14Shell", StringComparison.Ordinal))
            return tagId["N14Shell".Length..];

        return tagId;
    }

    /// <summary>
    ///     Считает патроны нужного калибра в инвентаре игрока, не включая те, что внутри данного оружия.
    /// </summary>
    private int CountAmmoInInventory(EntityUid actor, List<ProtoId<TagPrototype>> tags, EntityUid? excludeWeapon)
    {
        // Сущности внутри исключаемого оружия (его магазин/барабан), чтобы не считать их как "в рюкзаке".
        HashSet<EntityUid>? inside = null;

        if (excludeWeapon != null)
        {
            inside = new HashSet<EntityUid>();
            Collect(excludeWeapon.Value, inside, 0);
        }

        var total = 0;

        foreach (var ent in EnumerateInventory(actor))
        {
            if (inside != null && inside.Contains(ent))
                continue;

            if (!_tag.HasAnyTag(ent, tags))
                continue;

            if (TryComp<StackComponent>(ent, out var stack))
                total += stack.Count;
            else
                total++;
        }

        return total;
    }

    /// <summary>
    ///     По какой цене торговец берёт вещь у игрока: цена Wants, иначе StaticPrice.
    /// </summary>
    private bool TryGetSellPrice(N14MerchantComponent comp, string proto, out int price)
    {
        if (TryFindWant(comp, proto, out var want) && want.Price > 0)
        {
            price = want.Price;
            return true;
        }

        price = GetPrice(proto, 0);
        return price > 0;
    }

    /// <summary>
    ///     Купленная у игрока вещь попадает в ассортимент торговца с наценкой <see cref="MerchantMarkup"/>.
    /// </summary>
    private void AddToStock(N14MerchantComponent comp, string product, int boughtPrice)
    {
        var sellPrice = (int)Math.Round(boughtPrice * MerchantMarkup, MidpointRounding.AwayFromZero);

        for (var i = 0; i < comp.Stock.Count; i++)
        {
            var listing = comp.Stock[i];

            if (listing.Product.Id != product || listing.Remaining == 0)
                continue;

            // Докидываем штуку в уже существующую позицию, цену обновляем.
            listing.Remaining = listing.Remaining < 0 ? -1 : listing.Remaining + 1;
            listing.Price = sellPrice;
            comp.Stock[i] = listing;
            return;
        }

        comp.Stock.Add(new N14MerchantListing
        {
            Product = new EntProtoId(product),
            Price = sellPrice,
            Remaining = 1,
        });
    }

    #region Капсы

    private int CountCaps(EntityUid uid)
    {
        var total = 0;

        foreach (var ent in EnumerateInventory(uid))
        {
            if (TryComp<StackComponent>(ent, out var stack) && stack.StackTypeId == CapsStackId)
                total += stack.Count;
        }

        return total;
    }

    private bool TryTakeCaps(EntityUid uid, int amount)
    {
        if (amount <= 0)
            return true;

        if (CountCaps(uid) < amount)
            return false;

        foreach (var ent in EnumerateInventory(uid))
        {
            if (amount <= 0)
                break;

            if (!TryComp<StackComponent>(ent, out var stack) || stack.StackTypeId != CapsStackId)
                continue;

            if (stack.Count <= 0)
                continue;

            var take = Math.Min(stack.Count, amount);
            _stack.SetCount(ent, stack.Count - take, stack);
            amount -= take;
        }

        return amount <= 0;
    }

    private void GiveCaps(EntityUid uid, int amount)
    {
        if (amount <= 0)
            return;

        foreach (var ent in EnumerateInventory(uid))
        {
            if (amount <= 0)
                break;

            if (!TryComp<StackComponent>(ent, out var stack) || stack.StackTypeId != CapsStackId)
                continue;

            var max = _prototype.TryIndex<StackPrototype>(CapsStackId, out var proto) ? proto.MaxCount ?? int.MaxValue : amount;
            var space = max - stack.Count;
            if (space <= 0)
                continue;

            var add = Math.Min(space, amount);
            _stack.SetCount(ent, stack.Count + add, stack);
            amount -= add;
        }

        if (amount <= 0)
            return;

        var perStack = _prototype.TryIndex<StackPrototype>(CapsStackId, out var sp) ? Math.Max(1, sp.MaxCount ?? amount) : amount;
        var coords = Transform(uid).Coordinates;

        while (amount > 0)
        {
            var give = Math.Min(perStack, amount);
            var cap = Spawn("N14CurrencyCap", coords);

            if (TryComp<StackComponent>(cap, out var newStack))
                _stack.SetCount(cap, give, newStack);

            _hands.PickupOrDrop(uid, cap);
            amount -= give;
        }
    }

    #endregion

    #region Инвентарь

    /// <summary>
    ///     Сущности, вставленные в контейнер боеприпасов любого предмета инвентаря игрока
    ///     (спидлоадеры/магазины). Считаются частью оружия и не показываются отдельными строками.
    /// </summary>
    private HashSet<EntityUid> CollectInsertedMagazines(EntityUid actor)
    {
        var result = new HashSet<EntityUid>();

        foreach (var ent in EnumerateInventory(actor))
        {
            if (!TryComp<BallisticAmmoProviderComponent>(ent, out var ballistic))
                continue;

            foreach (var contained in ballistic.Container.ContainedEntities)
            {
                // Только магазины/спидлоадеры (у них есть свой провайдер), патроны не трогаем.
                if (TryComp<BallisticAmmoProviderComponent>(contained, out _))
                    result.Add(contained);
            }
        }

        return result;
    }

    private List<EntityUid> EnumerateInventory(EntityUid uid)
    {
        var result = new List<EntityUid>();

        foreach (var held in _hands.EnumerateHeld(uid))
        {
            result.Add(held);
            Collect(held, result, 0);
        }

        if (TryComp<InventoryComponent>(uid, out var inventory))
        {
            foreach (var slot in inventory.Containers)
            {
                foreach (var ent in slot.ContainedEntities)
                {
                    result.Add(ent);
                    Collect(ent, result, 0);
                }
            }
        }

        return result;
    }

    private void Collect(EntityUid uid, List<EntityUid> list, int depth)
    {
        if (depth > 8)
            return;

        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var ent in container.ContainedEntities)
            {
                list.Add(ent);
                Collect(ent, list, depth + 1);
            }
        }
    }

    /// <summary>
    ///     Собирает все сущности внутри контейнеров <paramref name="uid"/> в множество.
    /// </summary>
    private void Collect(EntityUid uid, HashSet<EntityUid> set, int depth)
    {
        if (depth > 8)
            return;

        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var ent in container.ContainedEntities)
            {
                set.Add(ent);
                Collect(ent, set, depth + 1);
            }
        }
    }

    private bool TryConsumeItem(EntityUid user, string proto)
    {
        foreach (var ent in EnumerateInventory(user))
        {
            if (!TryGetProtoId(ent, out var id) || id != proto)
                continue;

            Del(ent);
            return true;
        }

        return false;
    }

    #endregion
}
