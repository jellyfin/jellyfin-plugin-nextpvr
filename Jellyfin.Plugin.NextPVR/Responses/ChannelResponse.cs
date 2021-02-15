using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MediaBrowser.Controller.LiveTv;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.NextPVR.Helpers;
using System.Threading.Tasks;
using System.Text.Json;
using MediaBrowser.Common.Json;

namespace Jellyfin.Plugin.NextPVR.Responses
{
    public class ChannelResponse
    {
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.GetOptions();

        public ChannelResponse(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        public async Task<IEnumerable<ChannelInfo>> GetChannels(Stream stream, ILogger<LiveTvService> logger)
        {
            var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);

            if (root == null)
            {
                logger.LogError("Failed to download channel information.");
                throw new Exception("Failed to download channel information.");
            }

            if (root.channels != null)
            {
                UtilsHelper.DebugInformation(logger, string.Format("[NextPVR] ChannelResponse: {0}", JsonSerializer.Serialize(root, _jsonOptions)));
                return root.channels.Select(i => new ChannelInfo
                {
                    Name = i.channelName,
                    Number = i.channelNumberFormated,
                    Id = i.channelId.ToString(_usCulture),
                    ImageUrl = string.Format("{0}/service?method=channel.icon&channel_id={1}", _baseUrl, i.channelId),
                    ChannelType = ChannelHelper.GetChannelType(i.channelType),
                    HasImage = i.channelIcon
                });
            }

            return new List<ChannelInfo>();
        }
        // Classes created with http://json2csharp.com/
        public class Channel
        {
            public int channelId { get; set; }
            public int channelNumber { get; set; }
            public int channelMinor { get; set; }
            public string channelNumberFormated { get; set; }
            public int channelType { get; set; }
            public string channelName { get; set; }
            public string channelDetails { get; set; }
            public bool channelIcon { get; set; }
        }

        public class RootObject
        {
            public List<Channel> channels { get; set; }
        }
    }
    }
