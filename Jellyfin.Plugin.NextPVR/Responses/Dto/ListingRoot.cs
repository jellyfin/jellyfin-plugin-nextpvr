using System.Collections.Generic;

namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// The response to a guide listing request.
/// </summary>
internal sealed class ListingRoot
{
    public List<Listing>? Listings { get; set; }
}
