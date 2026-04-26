using Content.Shared._N14.Terminal;
using Robust.Server.GameObjects;

namespace Content.Server._N14.Terminal;

public sealed class N14TerminalSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<N14TerminalComponent, BoundUIOpenedEvent>(OnUIOpened);
    }

    private void OnUIOpened(EntityUid uid, N14TerminalComponent component, BoundUIOpenedEvent args)
    {
        UpdateState(uid, component);
    }

    private void UpdateState(EntityUid uid, N14TerminalComponent component)
    {
        var state = new N14TerminalBoundUserInterfaceState(
            component.TerminalTitle,
            component.Categories
        );

        _uiSystem.SetUiState(uid, N14TerminalUiKey.Key, state);
    }
}
