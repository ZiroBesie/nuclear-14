using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Forge.Merchant;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Forge.Merchant;

/// <summary>
///     Окно торговца: две колонки (Вещи / NPC), выбор стрелкой с ценой, Accept / Exit.
/// </summary>
public sealed class N14MerchantWindow : FancyWindow
{
    public event Action? OnAccept;
    public event Action? OnExit;

    private const string PreviewRsiPath = "/Textures/_NukeWake/icons/iconsguns.rsi";
    private const string PreviewStateId = "medved";

    private readonly N14MerchantColumn _playerColumn;
    private readonly N14MerchantColumn _merchantColumn;
    private readonly BoxContainer _middle;
    private readonly TextureRect _preview;
    private readonly N14MerchantInfoBox _priceBox;
    private readonly N14MerchantInfoBox _ammoBox;
    private readonly Control _previewPanel;
    private readonly BoxContainer _infoStack;
    private readonly N14MerchantDescBox _descBox;
    private readonly RichTextLabel _descLabel;
    private readonly BoxContainer _itemData;
    private readonly BoxContainer _columns;

    /// <summary>Ширина одного info-бокса (цена / калибр).</summary>
    private const float InfoBoxWidth = 150f;

    /// <summary>Ширина блока описания = сумма ширин двух info-боксов.</summary>
    private const float InfoWidth = InfoBoxWidth * 2f;

    /// <summary>Отступ нижнего края блока описания от низа окна.</summary>
    private const float ItemDataBottomGap = 40f;

    /// <summary>Внутренний отступ блока описания (рамка).</summary>
    private const float DescPadding = 4f;

    /// <summary>Отступ текста описания от боковых сторон блока.</summary>
    private const float DescTextInset = 10f;

    /// <summary>Диапазон размера шрифта описания (авто-подгонка под высоту полосы).</summary>
    private const int DescFontMax = 14;
    private const int DescFontMin = 7;

    /// <summary>Теги, разрешённые в описании (нужен font для авто-подгонки размера).</summary>
    private static readonly Type[] DescTags =
    {
        typeof(FontTag),
        typeof(ColorTag),
        typeof(BoldTag),
        typeof(ItalicTag),
    };

    /// <summary>Текущий текст описания (в markup-escape виде) для повторной укладки.</summary>
    private string _descText = string.Empty;

    /// <summary>Состояния предметов по прототипу — для отображения info-боксов при наведении.</summary>
    private readonly Dictionary<string, N14MerchantItemState> _itemsByProto = new();

    private readonly HashSet<int> _selectedOffers = new();
    private readonly HashSet<string> _selectedRequests = new();

    private readonly Font _font = N14MerchantTheme.GetFont(14);
    private readonly Font _infoFont = N14MerchantTheme.GetFont(12);

    public N14MerchantWindow()
    {
        Resizable = false;
        SetSize = new Vector2(820, 620);
        Title = Loc.GetString("n14-merchant-window-title");

        _playerColumn = new N14MerchantColumn(Loc.GetString("n14-merchant-column-items"));
        _merchantColumn = new N14MerchantColumn(Loc.GetString("n14-merchant-column-npc"));

        // Заголовки стоят на уровне верхней линии, но Items чуть левее, NPC чуть правее.
        _playerColumn.SetHeaderOffset(-18f);
        _merchantColumn.SetHeaderOffset(18f);

        _middle = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinWidth = 118,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(4, 0),
        };

        var columns = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };
        columns.AddChild(_playerColumn);
        columns.AddChild(_middle);
        columns.AddChild(_merchantColumn);
        _columns = columns;
        // Позиция/размер колонок меняются при пересборке строк — пересчитываем укладку описания.
        _columns.OnResized += LayoutItemData;

        // Спрайт-предпросмотр: висит под нижним краем левой колонки, по центру её ширины.
        _preview = new TextureRect
        {
            Texture = LoadPreviewTexture(),
            Modulate = N14MerchantTheme.Accent,
            MinSize = new Vector2(96f, 112f),
            MaxSize = new Vector2(96f, 112f),
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = Control.MouseFilterMode.Ignore,
            VerticalAlignment = VAlignment.Center,
        };

        // Info-боксы (аналог BM_ItemData в F:NV): цена и калибр (патроны в магазине / у игрока).
        _priceBox = new N14MerchantInfoBox(string.Empty, _infoFont) { MinWidth = InfoBoxWidth };
        _ammoBox = new N14MerchantInfoBox(string.Empty, _infoFont) { MinWidth = InfoBoxWidth };

        _infoStack = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        _infoStack.AddChild(_priceBox);
        _infoStack.AddChild(_ammoBox);

        // Большой блок описания предмета снизу окна: ширина = сумма ширин info-боксов.
        _descLabel = new RichTextLabel
        {
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Top,
            // Отступ текста от левой рамки (чтобы не был в притык).
            Margin = new Thickness(DescTextInset, 2, DescTextInset, 2),
        };

        _descBox = new N14MerchantDescBox
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
            },
            Margin = new Thickness(4),
            MinWidth = InfoWidth,
            MaxWidth = InfoWidth,
        };
        _descBox.AddChild(_descLabel);

        // Панель предпросмотра = только иконка; висит под нижним краем левой колонки, по её центру.
        var previewPanel = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Visible = false,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        previewPanel.AddChild(_preview);

        // Анкер к нижнему центру колонки: иконка (96) отцентрирована.
        LayoutContainer.SetAnchorLeft(previewPanel, 0.5f);
        LayoutContainer.SetAnchorRight(previewPanel, 0.5f);
        LayoutContainer.SetAnchorTop(previewPanel, 1f);
        LayoutContainer.SetAnchorBottom(previewPanel, 1f);
        LayoutContainer.SetMarginLeft(previewPanel, -48f);
        LayoutContainer.SetMarginRight(previewPanel, 48f);
        LayoutContainer.SetMarginTop(previewPanel, 18f);
        LayoutContainer.SetMarginBottom(previewPanel, 130f);

        _previewPanel = previewPanel;
        _playerColumn.AddChild(previewPanel);

        var accept = new Button
        {
            Text = Loc.GetString("n14-merchant-accept"),
            MinSize = new Vector2(130, 34),
            HorizontalAlignment = HAlignment.Right,
        };
        accept.Label.FontOverride = _font;
        accept.Label.FontColorOverride = N14MerchantTheme.Accent;
        accept.OnPressed += _ => OnAccept?.Invoke();

        var exit = new Button
        {
            Text = Loc.GetString("n14-merchant-exit"),
            MinSize = new Vector2(130, 34),
            HorizontalAlignment = HAlignment.Right,
        };
        exit.Label.FontOverride = _font;
        exit.Label.FontColorOverride = N14MerchantTheme.Accent;
        exit.OnPressed += _ =>
        {
            OnExit?.Invoke();
            Close();
        };

        var buttons = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Right,
            Margin = new Thickness(0, 0, 8, 4),
        };
        buttons.AddChild(accept);
        buttons.AddChild(exit);

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(16, 14, 16, 34),
        };

        var topSpacer = new Control { MinHeight = 8, VerticalExpand = true };
        var bottomSpacer = new Control { MinHeight = 8, VerticalExpand = true };

        root.AddChild(topSpacer);
        root.AddChild(columns);
        root.AddChild(bottomSpacer);
        root.AddChild(buttons);

        var backdrop = new N14MerchantBackdrop
        {
            // Нижняя линия фона выравнивается по краям колонок.
            LineTarget = columns,
        };

        var layout = new LayoutContainer();
        layout.AddChild(backdrop);
        layout.AddChild(root);

        // Низ окна: info-боксы (цена/калибр) — всегда на фиксированной высоте от низа,
        // описание — над ними и никогда не поднимается выше нижней линии колонок.
        var itemData = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Bottom,
            Visible = false,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        _itemData = itemData;

        itemData.AddChild(_descBox);
        itemData.AddChild(_infoStack);

        layout.AddChild(itemData);
        LayoutContainer.SetAnchorLeft(itemData, 0.5f);
        LayoutContainer.SetAnchorRight(itemData, 0.5f);
        LayoutContainer.SetAnchorTop(itemData, 1f);
        LayoutContainer.SetAnchorBottom(itemData, 1f);
        LayoutContainer.SetMarginLeft(itemData, -InfoWidth / 2f);
        LayoutContainer.SetMarginRight(itemData, InfoWidth / 2f);
        LayoutContainer.SetMarginTop(itemData, -2000f);
        LayoutContainer.SetMarginBottom(itemData, -ItemDataBottomGap);

        LayoutContainer.SetAnchorPreset(backdrop, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetAnchorPreset(root, LayoutContainer.LayoutPreset.Wide);

        XamlChildren.Add(layout);

        StripWindowChrome(this);
    }

    /// <summary>
    ///     Окно жёстко закреплено по центру экрана — перетаскивание отключено.
    /// </summary>
    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => DragMode.None;

    /// <summary>
    ///     Держим блок описания в границах доступной полосы: info-боксы всегда на фиксированной
    ///     высоте от низа, а описание над ними не поднимается выше нижней линии колонок.
    /// </summary>
    protected override void Resized()
    {
        base.Resized();
        LayoutItemData();
    }

    /// <summary>
    ///     Подгоняет размер шрифта описания так, чтобы весь текст помещался в полосу
    ///     между нижней линией колонок и info-боксами, не сдвигая последние.
    /// </summary>
    private void LayoutItemData()
    {
        if (_itemData == null || _descLabel == null)
            return;

        // Нижний край полосы описания = верх info-боксов; верх полосы = низ колонок.
        var itemDataBottom = GlobalPosition.Y + Size.Y - ItemDataBottomGap;
        var descriptionBottom = itemDataBottom - N14MerchantInfoBox.RowHeight;
        var columnsBottom = _columns.GlobalPosition.Y + _columns.Size.Y;

        // Доступная высота под описание (может быть отрицательной — тогда текст не влезает вовсе).
        var available = MathF.Max(0f, descriptionBottom - columnsBottom);

        FitDescriptionFont(available);
    }

    /// <summary>
    ///     Подбирает максимальный размер шрифта (от <see cref="DescFontMax"/> к <see cref="DescFontMin"/>),
    ///     при котором описание умещается по высоте в <paramref name="available"/> пикселей.
    /// </summary>
    private void FitDescriptionFont(float available)
    {
        if (string.IsNullOrEmpty(_descText))
        {
            _descBox.Visible = false;
            return;
        }

        _descBox.Visible = true;

        // Внутренняя ширина блока (минус рамка/паддинг и отступы текста).
        var innerWidth = InfoWidth - DescPadding * 2f - DescTextInset * 2f;

        for (var size = DescFontMax; size >= DescFontMin; size--)
        {
            _descLabel.SetMessage(DescMessage(size), DescTags, N14MerchantTheme.Accent);
            _descLabel.Measure(new Vector2(innerWidth, float.PositiveInfinity));

            if (_descLabel.DesiredSize.Y <= available)
            {
                _descBox.MaxHeight = float.PositiveInfinity;
                return;
            }
        }

        // Даже минимальный размер не влез — оставляем минимальный и ограничиваем высоту блока.
        _descLabel.SetMessage(DescMessage(DescFontMin), DescTags, N14MerchantTheme.Accent);
        _descLabel.Measure(new Vector2(innerWidth, float.PositiveInfinity));
        _descBox.MaxHeight = available;
    }

    /// <summary>Описание, обёрнутое в тег размера шрифта (для авто-подгонки).</summary>
    private FormattedMessage DescMessage(int size)
    {
        return FormattedMessage.FromMarkupPermissive($"[font size={size}]{_descText}[/font]");
    }

    /// <summary>
    ///     Убираем стандартную шапку окна (заголовок, крестик), разделитель под ней
    ///     и серый фон FancyWindow.
    /// </summary>
    private static void StripWindowChrome(Control root)
    {
        foreach (var child in root.Children.ToArray())
        {
            if (child is PanelContainer panel)
            {
                if (panel.StyleClasses.Contains("LowDivider") ||
                    panel.StyleClasses.Contains("AngleRect"))
                {
                    panel.Visible = false;
                    continue;
                }

                if (panel.StyleClasses.Contains("WindowHeadingBackground"))
                {
                    if (panel.Parent is Control header)
                        header.Visible = false;
                    else
                        panel.Visible = false;

                    continue;
                }
            }

            StripWindowChrome(child);
        }
    }

    public void UpdateState(N14MerchantUiState state)
    {
        Title = state.MerchantName;
        _merchantColumn.SetHeaderText(state.MerchantName);
        _playerColumn.Caps.Text = Loc.GetString("n14-merchant-caps", ("caps", state.PlayerCaps));
        _merchantColumn.Caps.Text = Loc.GetString("n14-merchant-caps", ("caps", state.MerchantCaps));

        // Пересобираем строки, сохраняя выбор по прототипу/индексу.
        _previewPanel.Visible = false;
        _itemData.Visible = false;
        _descText = string.Empty;
        _descBox.MaxHeight = float.PositiveInfinity;
        _itemsByProto.Clear();
        foreach (var item in state.Requests)
            _itemsByProto.TryAdd(item.Proto, item);
        foreach (var item in state.Offers)
            _itemsByProto.TryAdd(item.Proto, item);

        _playerColumn.Rows.RemoveAllChildren();
        foreach (var item in state.Requests)
        {
            var row = new N14MerchantRow(item.Proto, -1, item.Name, item.Price, item.Count, _font);
            row.Selected = _selectedRequests.Contains(item.Proto);
            row.OnClicked += OnRequestClicked;
            row.OnHovered += OnRowHovered;
            _playerColumn.Rows.AddChild(row);
        }

        _merchantColumn.Rows.RemoveAllChildren();
        foreach (var item in state.Offers)
        {
            var row = new N14MerchantRow(item.Proto, item.Index, item.Name, item.Price, item.Count, _font);
            row.Selected = _selectedOffers.Contains(item.Index);
            row.OnClicked += OnOfferClicked;
            row.OnHovered += OnRowHovered;
            _merchantColumn.Rows.AddChild(row);
        }

        // Отбрасываем выбор по позициям, которых больше нет.
        _selectedOffers.RemoveWhere(i => state.Offers.All(o => o.Index != i));
        _selectedRequests.RemoveWhere(p => state.Requests.All(r => r.Proto != p));

        RecenterWindow(new Vector2(0.5f, 0.5f));
        RebuildMiddle();
    }

    private void OnOfferClicked(N14MerchantRow row)
    {
        if (row.Selected)
            _selectedOffers.Add(row.OfferIndex);
        else
            _selectedOffers.Remove(row.OfferIndex);

        RebuildMiddle();
    }

    private void OnRequestClicked(N14MerchantRow row)
    {
        if (row.Selected)
            _selectedRequests.Add(row.Proto);
        else
            _selectedRequests.Remove(row.Proto);

        RebuildMiddle();
    }

    /// <summary>
    ///     Показывает спрайт-предпросмотр и info-боксы под левой колонкой, пока курсор на предмете.
    /// </summary>
    private void OnRowHovered(string? proto)
    {
        if (proto == null || !_itemsByProto.TryGetValue(proto, out var item))
        {
            _previewPanel.Visible = false;
            _itemData.Visible = false;
            return;
        }

        // Цена.
        _priceBox.SetText(Loc.GetString("n14-merchant-info-price", ("price", item.Price)));

        // Калибр и патроны — только если есть калибр.
        if (!string.IsNullOrEmpty(item.Caliber))
        {
            _ammoBox.SetText(Loc.GetString("n14-merchant-info-ammo",
                ("caliber", item.Caliber),
                ("mag", item.AmmoInMag),
                ("bag", item.AmmoInBag)));
            _ammoBox.Visible = true;
        }
        else
        {
            _ammoBox.Visible = false;
        }

        // Большой блок с описанием предмета: текст масштабируется, чтобы не сдвигать info-боксы.
        _descText = EscapeMarkup(item.Description);

        // Иконка и item data видны только для оружия с данными о калибре.
        var isWeapon = _ammoBox.Visible;
        _previewPanel.Visible = isWeapon;
        _itemData.Visible = isWeapon;

        if (isWeapon)
            LayoutItemData();
        else
            _descBox.Visible = false;
    }

    /// <summary>
    ///     Экранирует служебные для markup-разметки символы, чтобы описание рендерилось как текст.
    /// </summary>
    private static string EscapeMarkup(string text)
        => text.Replace("[", "[lb]");

    /// <summary>
    ///     Первый кадр состояния <c>medved</c> из RSI с иконками оружия (или null, если не нашли).
    /// </summary>
    private static Texture? LoadPreviewTexture()
    {
        var cache = IoCManager.Resolve<IResourceCache>();

        if (!cache.TryGetResource<RSIResource>(PreviewRsiPath, out var rsi))
            return null;

        if (!rsi.RSI.TryGetState(PreviewStateId, out var state))
            return null;

        return state.Frame0;
    }

    /// <summary>
    ///     Между колонками — по одной стрелке на направление с суммарной ценой.
    ///     Покупка — минус (тратим крышки), продажа — плюс (получаем крышки).
    /// </summary>
    private void RebuildMiddle()
    {
        _middle.RemoveAllChildren();

        // Суммируем выбранные позиции, чтобы не плодить стрелки с отдельными ценами.
        var buyTotal = 0;
        var sellTotal = 0;

        foreach (var row in _merchantColumn.Rows.Children)
        {
            if (row is N14MerchantRow offer && offer.Selected)
                buyTotal += offer.Price;
        }

        foreach (var row in _playerColumn.Rows.Children)
        {
            if (row is N14MerchantRow request && request.Selected)
                sellTotal += request.Price;
        }

        // Покупка у торговца: списываем крышки — знак «-».
        if (buyTotal > 0)
            _middle.AddChild(CreateIndicator(true, -buyTotal));

        // Продажа торговцу: получаем крышки — знак «+».
        if (sellTotal > 0)
            _middle.AddChild(CreateIndicator(false, sellTotal));
    }

    private Control CreateIndicator(bool pointRight, int price)
    {
        var group = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
        };

        if (price != 0)
        {
            group.AddChild(new Label
            {
                Text = price >= 0 ? $"+{price}" : price.ToString(),
                FontOverride = N14MerchantTheme.GetFont(14),
                FontColorOverride = N14MerchantTheme.Accent,
                HorizontalAlignment = HAlignment.Center,
            });
        }

        group.AddChild(new N14MerchantArrow(pointRight)
        {
            HorizontalAlignment = HAlignment.Center,
            Fill = N14MerchantTheme.Accent,
        });

        return group;
    }

    public (List<int> Buy, List<string> Sell) GetSelection()
        => (_selectedOffers.ToList(), _selectedRequests.ToList());

    public void ClearSelection()
    {
        _selectedOffers.Clear();
        _selectedRequests.Clear();

        foreach (var row in _merchantColumn.Rows.Children)
        {
            if (row is N14MerchantRow offer)
                offer.Selected = false;
        }

        foreach (var row in _playerColumn.Rows.Children)
        {
            if (row is N14MerchantRow request)
                request.Selected = false;
        }

        RebuildMiddle();
    }
}
