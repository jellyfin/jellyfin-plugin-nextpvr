using System.Collections.Generic;

namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// The response to a recurring recording listing request.
/// </summary>
internal sealed class RecurringRoot
{
    public List<Recurring>? Recurrings { get; set; }
}
