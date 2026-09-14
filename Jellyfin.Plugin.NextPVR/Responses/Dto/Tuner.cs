using System.Collections.Generic;

namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// A tuner known to NextPVR.
/// </summary>
internal sealed class Tuner
{
    public string TunerName { get; set; } = string.Empty;

    public string? TunerStatus { get; set; }

    public List<TunerRecordings>? Recordings { get; set; }

    public List<object>? LiveTv { get; set; }
}
