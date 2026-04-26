using Robust.Shared.Serialization;

namespace Content.Shared._N14.Terminal;

[Serializable, NetSerializable]
public enum N14TerminalUiKey : byte
{
    Key,
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class N14TerminalEntry
{
    [DataField]
    public string CategoryName = string.Empty;

    [DataField]
    public string Content = string.Empty;
}

[Serializable, NetSerializable]
public sealed class N14TerminalBoundUserInterfaceState : BoundUserInterfaceState
{
    public string Title;
    public List<N14TerminalEntry> Categories;

    public N14TerminalBoundUserInterfaceState(string title, List<N14TerminalEntry> categories)
    {
        Title = title;
        Categories = categories;
    }
}
