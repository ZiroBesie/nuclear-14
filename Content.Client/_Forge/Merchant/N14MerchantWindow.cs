using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Forge.Merchant;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
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
            MinWidth = 76,
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

        // Info-боксы справа от иконки: цена и калибр (патроны в магазине / у игрока).
        var infoFont = _infoFont;
        _priceBox = new N14MerchantInfoBox(string.Empty, infoFont) { MinWidth = 150f };
        _ammoBox = new N14MerchantInfoBox(string.Empty, infoFont) { MinWidth = 150f };

        var infoStack = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };
        infoStack.AddChild(_priceBox);
        infoStack.AddChild(_ammoBox);

        // Панель = иконка + info-боксы; висит под нижним краем колонки, иконка по центру колонки.
        var previewPanel = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Visible = false,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        previewPanel.AddChild(_preview);
        previewPanel.AddChild(infoStack);

        // Анкер к нижнему центру колонки: иконка (96) отцентрирована, боксы уходят вправо.
        LayoutContainer.SetAnchorLeft(previewPanel, 0.5f);
        LayoutContainer.SetAnchorRight(previewPanel, 0.5f);
        LayoutContainer.SetAnchorTop(previewPanel, 1f);
        LayoutContainer.SetAnchorBottom(previewPanel, 1f);
        LayoutContainer.SetMarginLeft(previewPanel, -48f);
        LayoutContainer.SetMarginRight(previewPanel, 164f);
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

        // Панель (картинка + боксы) видна только для оружия с данными о калибре.
        _previewPanel.Visible = _ammoBox.Visible;
    }

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
