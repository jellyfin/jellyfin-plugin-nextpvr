namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// A channel known to NextPVR.
/// </summary>
internal sealed class Channel
{
    public int ChannelId { get; set; }

    public int ChannelNumber { get; set; }

    public int ChannelMinor { get; set; }

    public string ChannelNumberFormated { get; set; } = string.Empty;

    public int ChannelType { get; set; }

    public string ChannelName { get; set; } = string.Empty;

    public string? ChannelDetails { get; set; }

    public bool ChannelIcon { get; set; }
}
