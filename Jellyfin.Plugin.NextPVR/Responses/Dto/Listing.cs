using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// A single programme in the guide.
/// </summary>
internal sealed class Listing
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public List<string>? Genres { get; set; }

    public bool Firstrun { get; set; }

    public string? Deferredartwork { get; set; }

    public int Start { get; set; }

    public int End { get; set; }

    public string Rating { get; set; } = string.Empty;

    public DateTime? Original { get; set; }

    public int? Season { get; set; }

    public int? Episode { get; set; }

    public int? Year { get; set; }

    public string? Significance { get; set; }

    public string? RecordingStatus { get; set; }

    public int RecordingId { get; set; }
}
