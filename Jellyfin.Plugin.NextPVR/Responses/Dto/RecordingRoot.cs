using System.Collections.Generic;

namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// The response to a recording listing request.
/// </summary>
internal sealed class RecordingRoot
{
    public List<Recording>? Recordings { get; set; }
}
