using System;
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

public class ListingsResponse
{
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;
    private string _channelId;

    public ListingsResponse(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    public async Task<IEnumerable<ProgramInfo>> GetPrograms(Stream stream, string channelId, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(logger, $"GetPrograms Response: {JsonSerializer.Serialize(root, _jsonOptions)}");
        _channelId = channelId;
        return root.Listings
            .Select(i => i)
            .Select(GetProgram);
    }

    private ProgramInfo GetProgram(Listing epg)
    {
        var genreMapper = new GenreMapper(Plugin.Instance.Configuration);

        string backgroundUrl = Plugin.Instance.Configuration.GetEpisodeImage ? $"{_baseUrl}/service?method=channel.show.artwork&prefer=landscape&name={Uri.EscapeDataString(epg.Name)}" : null;
        if (!string.IsNullOrEmpty(epg.Deferredartwork))
        {
            backgroundUrl = epg.Deferredartwork;
        }

        var info = new ProgramInfo
        {
            ChannelId = _channelId,
            Id = epg.Id.ToString(CultureInfo.InvariantCulture),
            Overview = epg.Description,
            EpisodeTitle = epg.Subtitle,
            SeasonNumber = epg.Season,
            EpisodeNumber = epg.Episode,
            StartDate = DateTimeOffset.FromUnixTimeSeconds(epg.Start).DateTime,
            EndDate = DateTimeOffset.FromUnixTimeSeconds(epg.End).DateTime,
            Genres = new List<string>(), // epg.genres.Where(g => !string.IsNullOrWhiteSpace(g)).ToList(),
            OriginalAirDate = epg.Original == null ? epg.Original : DateTime.SpecifyKind((DateTime)epg.Original, DateTimeKind.Local),
            ProductionYear = epg.Year,
            Name = epg.Name,
            OfficialRating = epg.Rating,
            IsPremiere = epg.Significance != null && epg.Significance.Contains("Premiere", StringComparison.OrdinalIgnoreCase),
            // CommunityRating = ParseCommunityRating(epg.StarRating),
            // Audio = ParseAudio(epg.Audio),
            // IsHD = string.Equals(epg.Quality, "hdtv", StringComparison.OrdinalIgnoreCase),
            IsLive = epg.Significance != null && epg.Significance.Contains("Live", StringComparison.OrdinalIgnoreCase),
            IsRepeat = !Plugin.Instance.Configuration.ShowRepeat || !epg.Firstrun,
            IsSeries = true, // !string.IsNullOrEmpty(epg.Subtitle),  http://emby.media/community/index.php?/topic/21264-series-record-ability-missing-in-emby-epg/#entry239633
            HasImage = Plugin.Instance.Configuration.GetEpisodeImage,
            ImageUrl = Plugin.Instance.Configuration.GetEpisodeImage ? $"{_baseUrl}/service?method=channel.show.artwork&name={Uri.EscapeDataString(epg.Name)}" : null,
            BackdropImageUrl = backgroundUrl
        };

        if (epg.Genres != null)
        {
            info.Genres = epg.Genres;
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

    private sealed class Listing
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Subtitle { get; set; }

        public List<string> Genres { get; set; }

        public bool Firstrun { get; set; }

        public string Deferredartwork { get; set; }

        public int Start { get; set; }

        public int End { get; set; }

        public string Rating { get; set; }

        public DateTime? Original { get; set; }

        public int? Season { get; set; }

        public int? Episode { get; set; }

        public int? Year { get; set; }

        public string Significance { get; set; }

        public string RecordingStatus { get; set; }

        public int RecordingId { get; set; }
    }

    private sealed class RootObject
    {
        public List<Listing> Listings { get; set; }
    }
}
