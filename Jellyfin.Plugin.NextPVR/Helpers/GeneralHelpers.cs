using MediaBrowser.Model.LiveTv;
using Microsoft.Extensions.Logging;

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

public static class UtilsHelper
{
    public static void DebugInformation(ILogger<LiveTvService> logger, string message)
    {
        var config = Plugin.Instance.Configuration;
        bool enableDebugLogging = config.EnableDebugLogging;

        if (enableDebugLogging)
        {
#pragma warning disable CA2254
            logger.LogDebug(message);
#pragma warning restore CA2254
        }
    }
}
