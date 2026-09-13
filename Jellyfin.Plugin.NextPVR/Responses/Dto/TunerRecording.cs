namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// A recording a tuner is currently making.
/// </summary>
internal sealed class TunerRecording
{
    public int TunerOid { get; set; }

    public string? RecName { get; set; }

    public int ChannelOid { get; set; }

    public int RecordingOid { get; set; }
}
