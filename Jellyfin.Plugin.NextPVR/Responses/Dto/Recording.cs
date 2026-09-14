using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// A recording, either completed, in progress or still pending.
/// </summary>
internal sealed class Recording
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Desc { get; set; }

    public string? Subtitle { get; set; }

    public int StartTime { get; set; }

    public int Duration { get; set; }

    public int? Season { get; set; }

    public int? Episode { get; set; }

    public int EpgEventId { get; set; }

    public List<string>? Genres { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Rating { get; set; } = string.Empty;

    public string? Quality { get; set; }

    public string? Channel { get; set; }

    public int ChannelId { get; set; }

    public bool Blue { get; set; }

    public bool Green { get; set; }

    public bool Yellow { get; set; }

    public bool Red { get; set; }

    public int PrePadding { get; set; }

    public int PostPadding { get; set; }

    public string? File { get; set; }

    public int PlaybackPosition { get; set; }

    public bool Played { get; set; }

    public bool Recurring { get; set; }

    public int RecurringParent { get; set; }

    public bool Firstrun { get; set; }

    public string? Reason { get; set; }

    public string? Significance { get; set; }

    public DateTime? Original { get; set; }

    public int? Year { get; set; }
}
