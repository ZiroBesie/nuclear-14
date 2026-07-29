using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._N14.Pipboy;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class N14PipboyComponent : Component
{
    /// <summary>Player-created notes, persisted server-side.</summary>
    [DataField]
    public List<string> Notes = new();

    /// <summary>Rate-limit timer (server only).</summary>
    [ViewVariables]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>Currently playing audio stream entity. Networked so the client can track playback.</summary>
    [AutoNetworkedField]
    public EntityUid? AudioStream;

    /// <summary>Prototype ID of the selected jukebox song.</summary>
    [AutoNetworkedField]
    public string? SelectedSongId;
}
