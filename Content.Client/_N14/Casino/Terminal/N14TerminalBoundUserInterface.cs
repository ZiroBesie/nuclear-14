using Content.Shared._N14.Terminal;
using Robust.Client.UserInterface;

namespace Content.Client._N14.Terminal;

public sealed class N14TerminalBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private N14TerminalWindow? _window;

    public N14TerminalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<N14TerminalWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not N14TerminalBoundUserInterfaceState terminalState)
            return;

        _window?.Populate(terminalState.Title, terminalState.Categories);
    }
}
