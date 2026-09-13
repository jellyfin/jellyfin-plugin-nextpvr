using MediaBrowser.Model.LiveTv;

namespace Jellyfin.Plugin.NextPVR.Helpers;

/// <summary>
/// Provides methods to translate NextPVR channel details.
/// </summary>
public static class ChannelHelper
{
    /// <summary>
    /// Translates a NextPVR channel type into the equivalent Jellyfin <see cref="ChannelType"/>.
    /// </summary>
    /// <param name="channelType">The NextPVR channel type.</param>
    /// <returns>The matching <see cref="ChannelType"/>, defaulting to <see cref="ChannelType.TV"/>.</returns>
    public static ChannelType GetChannelType(int channelType)
    {
        ChannelType type = channelType switch
        {
            1 => ChannelType.TV,
            10 => ChannelType.Radio,
            _ => ChannelType.TV
        };

        return type;
    }
}
