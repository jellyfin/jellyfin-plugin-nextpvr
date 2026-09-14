namespace Jellyfin.Plugin.NextPVR.Responses.Dto;

/// <summary>
/// The response to a session validation or login request.
/// </summary>
internal sealed class SessionRoot
{
    public string? Stat { get; set; }

    public string? Sid { get; set; }
}
