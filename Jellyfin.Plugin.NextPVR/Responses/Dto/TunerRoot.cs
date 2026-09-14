using System.Collections.Generic;

namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// The response to a tuner listing request.
/// </summary>
internal sealed class TunerRoot
{
    public List<Tuner>? Tuners { get; set; }
}
