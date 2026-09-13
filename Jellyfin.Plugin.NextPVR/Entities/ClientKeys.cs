namespace Jellyfin.Plugin.NextPVR.Entities;

/// <summary>
/// The keys returned by NextPVR when a session is initiated.
/// </summary>
public class ClientKeys
{
    /// <summary>
    /// Gets or sets the id of the newly initiated session.
    /// </summary>
    public string Sid { get; set; }

    /// <summary>
    /// Gets or sets the salt used to hash the PIN when logging the session in.
    /// </summary>
    public string Salt { get; set; }
}
