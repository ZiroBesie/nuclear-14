using Content.Shared._N14.Terminal;
using Robust.Shared.GameObjects;

namespace Content.Server._N14.Terminal;

[RegisterComponent]
public sealed partial class N14TerminalComponent : Component
{
    [DataField]
    public string TerminalTitle = "ROBCO INDUSTRIES UNIFIED OPERATING SYSTEM";

    [DataField]
    public List<N14TerminalEntry> Categories = new();
}
