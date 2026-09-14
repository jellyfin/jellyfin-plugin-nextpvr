using System.Collections.Generic;

namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// The response to a channel listing request.
/// </summary>
internal sealed class ChannelRoot
{
    public List<Channel>? Channels { get; set; }
}
