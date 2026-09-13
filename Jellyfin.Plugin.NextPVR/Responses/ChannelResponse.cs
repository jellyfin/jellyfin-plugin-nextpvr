using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Helpers;
using Jellyfin.Plugin.NextPVR.Responses.Dto;
using MediaBrowser.Controller.LiveTv;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

/// <summary>
/// Reads the response to a channel listing request.
/// </summary>
public class ChannelResponse
{
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelResponse"/> class.
    /// </summary>
    /// <param name="baseUrl">The base URL of the NextPVR web service, used to build channel image URLs.</param>
    public ChannelResponse(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Reads the available channels.
    /// </summary>
    /// <param name="stream">The response stream to read.</param>
    /// <param name="logger">The logger to write diagnostic output to.</param>
    /// <returns>The channels reported by the backend.</returns>
    public async Task<IEnumerable<ChannelInfo>> GetChannels(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<ChannelRoot>(stream, _jsonOptions).ConfigureAwait(false);

        if (root is null)
        {
            logger.LogError("Failed to download channel information");
            throw new JsonException("Failed to download channel information.");
        }

        if (root.Channels is not null)
        {
            UtilsHelper.DebugInformation(logger, $"ChannelResponse: {JsonSerializer.Serialize(root, _jsonOptions)}");
            return root.Channels.Select(i => new ChannelInfo
            {
                Name = i.ChannelName,
                Number = i.ChannelNumberFormated,
                Id = i.ChannelId.ToString(CultureInfo.InvariantCulture),
                ImageUrl = $"{_baseUrl}/service?method=channel.icon&channel_id={i.ChannelId}",
                ChannelType = ChannelHelper.GetChannelType(i.ChannelType),
                HasImage = i.ChannelIcon
            });
        }

        return new List<ChannelInfo>();
    }

    // Classes created with http://json2csharp.com/
}
