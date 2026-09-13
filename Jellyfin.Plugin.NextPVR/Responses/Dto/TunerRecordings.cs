namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// A wrapper around the recording a tuner is currently making.
/// </summary>
internal sealed class TunerRecordings
{
    public TunerRecording? Recording { get; set; }
}
