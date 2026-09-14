namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// The response to a request that cancels or deletes a recording.
/// </summary>
internal sealed class RecordingErrorRoot
{
    public string? Stat { get; set; }
}
