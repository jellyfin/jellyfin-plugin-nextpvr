using MediaBrowser.Model.LiveTv;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Helpers;

/// <summary>
/// Provides shared helper methods for the plugin.
/// </summary>
public static class UtilsHelper
{
    /// <summary>
    /// Writes a message to the debug log, if debug logging is enabled in the plugin configuration.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">The message to write.</param>
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
