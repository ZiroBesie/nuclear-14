using Content.Shared._N14.BarterNpc;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._N14.BarterNpc;

[UsedImplicitly]
public sealed class BarterNpcBoundUserInterface : BoundUserInterface
{
    private BarterNpcMenu? _window;

    public BarterNpcBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BarterNpcMenu>();
        _window.OnTradePressed += index => SendMessage(new BarterExecuteMessage(index));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is BarterNpcBoundUiState s)
            _window?.Populate(s.Trades);
    }
}
