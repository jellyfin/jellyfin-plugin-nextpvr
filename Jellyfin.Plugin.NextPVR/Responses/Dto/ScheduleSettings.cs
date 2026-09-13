using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// The scheduling defaults of the NextPVR backend.
/// </summary>
internal sealed class ScheduleSettings
{
    public string? Version { get; set; }

    [JsonPropertyName("nextPVRVersion")]
    public int NextPvrVersion { get; set; }

    public string? ReadableVersion { get; set; }

    public bool LiveTimeshift { get; set; }

    public bool LiveTimeshiftBufferInfo { get; set; }

    public bool ChannelsUseSegmenter { get; set; }

    public bool RecordingsUseSegmenter { get; set; }

    public int WhatsNewDays { get; set; }

    public int SkipForwardSeconds { get; set; }

    public int SkipBackSeconds { get; set; }

    public int SkipFfSeconds { get; set; }

    public int SkipRwSeconds { get; set; }

    public string? RecordingView { get; set; }

    public int PrePadding { get; set; }

    public int PostPadding { get; set; }

    public bool ConfirmOnDelete { get; set; }

    public bool ShowNewInGuide { get; set; }

    public int SlipSeconds { get; set; }

    public string? RecordingDirectories { get; set; }

    public bool ChannelDetailsLevel { get; set; }

    public string? Time { get; set; }

    public int TimeEpoch { get; set; }
}
