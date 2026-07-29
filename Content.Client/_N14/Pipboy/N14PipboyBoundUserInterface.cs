using Content.Shared._N14.Pipboy;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._N14.Pipboy;

[UsedImplicitly]
public sealed class N14PipboyBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private N14PipboyMenu? _menu;

    public N14PipboyBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<N14PipboyMenu>();
        _menu.PipboyEntity = Owner;
        _menu.OpenCentered();

        // Notes
        _menu.OnAddNote    += text => SendMessage(new N14PipboyAddNoteMessage(text));
        _menu.OnDeleteNote += idx  => SendMessage(new N14PipboyDeleteNoteMessage(idx));

        // Radio
        _menu.OnRadioSongSelected += id   => SendMessage(new N14PipboyRadioSelectMessage(id));
        _menu.OnRadioPlayPressed  += play => SendMessage(new N14PipboyRadioPlayMessage(play));
        _menu.OnRadioStopPressed  += ()   => SendMessage(new N14PipboyRadioStopMessage());
        _menu.OnRadioSetTime      += t    => SendMessage(new N14PipboyRadioSetTimeMessage(t));

        // Inventory interaction
        _menu.OnPickupItem += netId => SendMessage(new N14PipboyPickupItemMessage(netId));

        // Ask the server for a fresh snapshot immediately
        SendMessage(new N14PipboyRequestUpdateMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not N14PipboyUpdateState pipState)
            return;
        _menu?.UpdateState(pipState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _menu?.Close();
        _menu = null;
    }
}
