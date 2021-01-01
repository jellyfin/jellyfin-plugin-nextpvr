using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.NextPVR.Helpers;
using System.Threading.Tasks;
using System.Text.Json;
using MediaBrowser.Common.Json;

namespace Jellyfin.Plugin.NextPVR.Responses
{
    class ListingsResponse
    {
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");
        private readonly string _baseUrl;
        private string _channelId;

        public ListingsResponse(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        public async Task<IEnumerable<ProgramInfo>> GetPrograms(Stream stream, string channelId, ILogger<LiveTvService> logger)
        {
            var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, JsonDefaults.GetOptions()).ConfigureAwait(false);
            UtilsHelper.DebugInformation(logger, string.Format("[NextPVR] GetPrograms Response: {0}", JsonSerializer.Serialize(root, JsonDefaults.GetOptions())));

            /*

            return listings.Where(i => string.Equals(i.Channel.channelOID.ToString(_usCulture), channelId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(i => i.EPGEvents.Select(e => GetProgram(i.Channel, e.epgEventJSONObject.epgEvent)));
            */
            _channelId = channelId;
            return root.listings
               .Select(i => i)
               .Select(GetProgram);
        }

        private ProgramInfo GetProgram(Listing epg)
        {
            var genreMapper = new GenreMapper(Plugin.Instance.Configuration);
            var info = new ProgramInfo
            {
                ChannelId = _channelId,
                Id = epg.id.ToString(),
                Overview = epg.description,
                EpisodeTitle = epg.subtitle,
                SeasonNumber = epg.season,
                EpisodeNumber = epg.episode,
                StartDate = DateTimeOffset.FromUnixTimeSeconds(epg.start).DateTime,
                EndDate = DateTimeOffset.FromUnixTimeSeconds(epg.end).DateTime,
                Genres = new List<string>(), // epg.genres.Where(g => !string.IsNullOrWhiteSpace(g)).ToList(),
                OriginalAirDate = epg.original == null ? epg.original : DateTime.SpecifyKind((System.DateTime)epg.original, DateTimeKind.Local),
                ProductionYear = epg.year,
                Name = epg.name,
                OfficialRating = epg.rating,
                IsPremiere = epg.significance != null ? epg.significance.Contains("Premiere") : false,
                //CommunityRating = ParseCommunityRating(epg.StarRating),
                //Audio = ParseAudio(epg.Audio),
                //IsHD = string.Equals(epg.Quality, "hdtv", StringComparison.OrdinalIgnoreCase),
                IsLive = epg.significance != null ? epg.significance.Contains("Live") : false,
                IsRepeat = Plugin.Instance.Configuration.ShowRepeat ? !epg.firstrun : true,
                IsSeries = true, //!string.IsNullOrEmpty(epg.Subtitle),  http://emby.media/community/index.php?/topic/21264-series-record-ability-missing-in-emby-epg/#entry239633
                HasImage = Plugin.Instance.Configuration.GetEpisodeImage,
                ImageUrl = Plugin.Instance.Configuration.GetEpisodeImage ? string.Format("{0}/service?method=channel.show.artwork&name={1}", _baseUrl, Uri.EscapeDataString(epg.name)) : null,
                BackdropImageUrl = Plugin.Instance.Configuration.GetEpisodeImage ? string.Format("{0}/service?method=channel.show.artwork&prefer=landscape&name={1}", _baseUrl, Uri.EscapeDataString(epg.name)) : null,
            };

            if (epg.genres != null)
            {
                info.Genres = epg.genres;
                info.Genres[0] += " Movie";
                genreMapper.PopulateProgramGenres(info);
                if (info.IsMovie)
                {
                    info.IsRepeat = false;
                    info.IsSeries = false;
                }
            }
            return info;
        }

        // Classes created with http://json2csharp.com/

        public class Listing
        {
            public int id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string subtitle { get; set; }
            public List<string> genres { get; set; }
            public bool firstrun { get; set; }
            public int start { get; set; }
            public int end { get; set; }
            public string rating { get; set; }
            public DateTime? original { get; set; }
            public int? season { get; set; }
            public int? episode { get; set; }
            public int? year { get; set; }
            public string significance { get; set; }
            public string recording_status { get; set; }
            public int recording_id { get; set; }
        }

        public class RootObject
        {
            public List<Listing> listings { get; set; }
        }

    }
}
