using MediaBrowser.Model.LiveTv;
using System;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Helpers
{
    public static class ChannelHelper
    {
        public static ChannelType GetChannelType(int channelType)
        {
            ChannelType type = new ChannelType();

            if (channelType == 1)
            {
                type = ChannelType.TV;
            }
            else if (channelType == 10)
            {
                type = ChannelType.Radio;
            }

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
                logger.LogDebug(message);
            }
        }

    }
}
