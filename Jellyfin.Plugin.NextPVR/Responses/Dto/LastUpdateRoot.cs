using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// The response describing when the recordings last changed.
/// </summary>
internal sealed class LastUpdateRoot
{
    [JsonPropertyName("last_update")]
    public int LastUpdate { get; set; }

    public string? Stat { get; set; }

    public int Code { get; set; }

    public string? Msg { get; set; }
}
