using System.Numerics;
using Content.Shared._N14.BarterNpc;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Client._N14.BarterNpc;

/// <summary>
/// Карточка одного обмена в стиле торгомата: тёмный фон, золотая рамка, иконки.
/// </summary>
public sealed class BarterTradeCard : PanelContainer
{
    public event Action? OnTradePressed;

    private static readonly StyleBoxFlat CardBox = new()
    {
        BackgroundColor = Color.FromHex("#141417E6"),
        BorderColor = Color.FromHex("#B08D3B"),
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = 10,
        ContentMarginRightOverride = 10,
        ContentMarginTopOverride = 8,
        ContentMarginBottomOverride = 8,
    };

    private static readonly StyleBoxFlat DividerBox = new()
    {
        BackgroundColor = Color.FromHex("#B08D3BCC"),
    };

    public BarterTradeCard(BarterEntryState trade, IPrototypeManager proto)
    {
        PanelOverride = CardBox;
        HorizontalExpand = true;

        var giveName = proto.TryIndex<EntityPrototype>(trade.GiveItem, out var giveProto)
            ? giveProto.Name : trade.GiveItem;
        var receiveName = proto.TryIndex<EntityPrototype>(trade.ReceiveItem, out var receiveProto)
            ? receiveProto.Name : trade.ReceiveItem;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };

        // --- Заголовок ---
        root.AddChild(new Label
        {
            Text = $"{giveName} → {receiveName}",
            StyleClasses = { "LabelHeading" },
            HorizontalExpand = true,
            Margin = new Thickness(2, 0, 2, 2),
        });

        // --- Золотой разделитель ---
        root.AddChild(new PanelContainer
        {
            MinSize = new Vector2(0, 1),
            HorizontalExpand = true,
            PanelOverride = DividerBox,
        });

        // --- Строка: иконка | стрелка | иконка (все по центру вертикали) ---
        var iconRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 16,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 8, 0, 4),
        };

        iconRow.AddChild(BuildIcon(trade.GiveItem, HAlignment.Right));

        iconRow.AddChild(new Label
        {
            Text = "→",
            StyleClasses = { "LabelHeading" },
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            MinWidth = 24,
        });

        iconRow.AddChild(BuildIcon(trade.ReceiveItem, HAlignment.Left));

        root.AddChild(iconRow);

        // --- Строка подписей под иконками ---
        var labelRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
            Margin = new Thickness(0, 0, 0, 6),
        };

        labelRow.AddChild(new Label
        {
            Text = trade.GiveCount > 1 ? $"{giveName} x{trade.GiveCount}" : giveName,
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            ClipText = true,
        });

        labelRow.AddChild(new Label
        {
            Text = trade.ReceiveCount > 1 ? $"{receiveName} x{trade.ReceiveCount}" : receiveName,
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            ClipText = true,
        });

        root.AddChild(labelRow);

        // --- Кнопка ---
        var btn = new Button
        {
            Text = Loc.GetString("barter-npc-button"),
            StyleClasses = { "ButtonBig" },
            HorizontalExpand = true,
            MinHeight = 36,
        };
        btn.OnPressed += _ => OnTradePressed?.Invoke();
        root.AddChild(btn);

        AddChild(root);
    }

    private static Control BuildIcon(string protoId, HAlignment align)
    {
        var container = new PanelContainer
        {
            MinSize = new Vector2(64, 64),
            MaxSize = new Vector2(64, 64),
            HorizontalExpand = true,
            HorizontalAlignment = align,
            VerticalAlignment = VAlignment.Center,
        };

        var icon = new EntityPrototypeView
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Scale = new Vector2(2f, 2f),
        };
        icon.SetPrototype(protoId);

        container.AddChild(icon);
        return container;
    }
}
