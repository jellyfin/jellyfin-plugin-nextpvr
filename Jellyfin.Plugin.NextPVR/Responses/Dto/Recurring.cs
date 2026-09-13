namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// A rule that records a programme repeatedly.
/// </summary>
internal sealed class Recurring
{
    public int Id { get; set; }

    public int Type { get; set; }

    public string? Name { get; set; }

    public int ChannelId { get; set; }

    public string? Channel { get; set; }

    public string? Period { get; set; }

    public int Keep { get; set; }

    public int PrePadding { get; set; }

    public int PostPadding { get; set; }

    public string EpgTitle { get; set; } = string.Empty;

    public string? DirectoryId { get; set; }

    public string? Days { get; set; }

    public bool Enabled { get; set; }

    public bool OnlyNewEpisodes { get; set; }

    public int StartTimeTicks { get; set; }

    public int EndTimeTicks { get; set; }

    public string? AdvancedRules { get; set; }
}
