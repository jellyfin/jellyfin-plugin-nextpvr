using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Helpers;
using MediaBrowser.Controller.LiveTv;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

public class ChannelResponse
{
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    public ChannelResponse(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    public async Task<IEnumerable<ChannelInfo>> GetChannels(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);

        if (root == null)
        {
            logger.LogError("Failed to download channel information");
            throw new JsonException("Failed to download channel information.");
        }

        if (root.Channels != null)
        {
            UtilsHelper.DebugInformation(logger, $"[NextPVR] ChannelResponse: {JsonSerializer.Serialize(root, _jsonOptions)}");
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
    private sealed class Channel
    {
        public int ChannelId { get; set; }

        public int ChannelNumber { get; set; }

        public int ChannelMinor { get; set; }

        public string ChannelNumberFormated { get; set; }

        public int ChannelType { get; set; }

        public string ChannelName { get; set; }

        public string ChannelDetails { get; set; }

        public bool ChannelIcon { get; set; }
    }

    private sealed class RootObject
    {
        public List<Channel> Channels { get; set; }
    }
}
