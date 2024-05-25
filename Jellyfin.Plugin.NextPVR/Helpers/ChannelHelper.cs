using MediaBrowser.Model.LiveTv;

namespace Jellyfin.Plugin.NextPVR.Helpers;

public static class ChannelHelper
{
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
