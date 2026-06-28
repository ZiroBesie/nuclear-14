using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._N14.BarterNpc;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Client._N14.BarterNpc;

public sealed class BarterNpcMenu : FancyWindow
{
    public event Action<int>? OnTradePressed;

    private readonly BoxContainer _tradeList;

    public BarterNpcMenu()
    {
        Title = Loc.GetString("barter-npc-title");
        MinWidth = 700;
        MinHeight = 500;
        SetSize = new Vector2(700, 500);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            StyleClasses = { "PanelBackgroundBase" },
        };

        // Заголовок
        var header = new PanelContainer
        {
            StyleClasses = { "PanelBackgroundBaseDark" },
            Margin = new Thickness(4, 4, 4, 0),
        };
        header.AddChild(new Label
        {
            Text = Loc.GetString("barter-npc-header"),
            StyleClasses = { "LabelHeading" },
            Margin = new Thickness(8, 6, 8, 6),
        });
        root.AddChild(header);

        // Список карточек
        _tradeList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(6),
        };

        var scroll = new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
        };
        scroll.AddChild(_tradeList);

        var listPanel = new PanelContainer
        {
            VerticalExpand = true,
            StyleClasses = { "PanelBackgroundBaseDark" },
            Margin = new Thickness(4),
        };
        listPanel.AddChild(scroll);
        root.AddChild(listPanel);

        ContentsContainer.AddChild(root);
    }

    public void Populate(List<BarterEntryState> trades)
    {
        _tradeList.Children.Clear();

        var proto = IoCManager.Resolve<IPrototypeManager>();

        if (trades.Count == 0)
        {
            _tradeList.AddChild(new Label
            {
                Text = Loc.GetString("barter-npc-empty"),
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 16),
            });
            return;
        }

        for (var i = 0; i < trades.Count; i++)
        {
            var idx = i;
            var card = new BarterTradeCard(trades[i], proto);
            card.OnTradePressed += () => OnTradePressed?.Invoke(idx);
            _tradeList.AddChild(card);
        }
    }
}
