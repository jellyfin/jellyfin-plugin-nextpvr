using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Entities;
using Jellyfin.Plugin.NextPVR.Helpers;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

/// <summary>
/// Reads the response to a recording listing request.
/// </summary>
public class RecordingResponse
{
    private readonly string _baseUrl;
    private readonly ILogger<LiveTvService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingResponse"/> class.
    /// </summary>
    /// <param name="baseUrl">The base URL of the NextPVR web service, used to build playback and artwork URLs.</param>
    /// <param name="logger">The logger to write diagnostic output to.</param>
    public RecordingResponse(string baseUrl, ILogger<LiveTvService> logger)
    {
        _baseUrl = baseUrl;
        _logger = logger;
    }

    /// <summary>
    /// Reads the completed and in-progress recordings, skipping any that failed or conflicted.
    /// </summary>
    /// <param name="stream">The response stream to read.</param>
    /// <returns>The recordings reported by the backend.</returns>
    public async Task<IReadOnlyList<MyRecordingInfo>> GetRecordings(Stream stream)
    {
        if (stream is null)
        {
            _logger.LogError("GetRecording stream is null");
            throw new ArgumentNullException(nameof(stream));
        }

        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(_logger, $"GetRecordings Response: {JsonSerializer.Serialize(root, _jsonOptions)}");

        if (root?.Recordings is null)
        {
            _logger.LogError("Failed to download the recordings");
            throw new JsonException("Failed to download the recordings.");
        }

        IEnumerable<MyRecordingInfo> recordings;
        try
        {
            recordings = root.Recordings
                .Select(i => i)
                .Where(i => !string.Equals(i.Status, "failed", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(i.Status, "conflict", StringComparison.OrdinalIgnoreCase))
                .Select(GetRecordingInfo);
        }
        catch (Exception err)
        {
            _logger.LogWarning(err, "Get recordings");
            throw;
        }

        return recordings.ToList();
    }

    /// <summary>
    /// Reads the pending recordings as timers.
    /// </summary>
    /// <param name="stream">The response stream to read.</param>
    /// <returns>The timers reported by the backend.</returns>
    public async Task<IEnumerable<TimerInfo>> GetTimers(Stream stream)
    {
        if (stream is null)
        {
            _logger.LogError("GetTimers stream is null");
            throw new ArgumentNullException(nameof(stream));
        }

        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(_logger, $"GetTimers Response: {JsonSerializer.Serialize(root, _jsonOptions)}");
        if (root?.Recordings is null)
        {
            _logger.LogError("Failed to download the timers");
            throw new JsonException("Failed to download the timers.");
        }

        IEnumerable<TimerInfo> timers;
        try
        {
            timers = root.Recordings
                .Select(i => i)
                .Select(GetTimerInfo);
        }
        catch (Exception err)
        {
            _logger.LogWarning(err, "Get timers");
            throw;
        }

        return timers;
    }

    private MyRecordingInfo GetRecordingInfo(Recording i)
    {
        var genreMapper = new GenreMapper(Plugin.Instance.Configuration);
        var info = new MyRecordingInfo
        {
            Id = i.Id.ToString(CultureInfo.InvariantCulture)
        };
        if (i.Recurring)
        {
            info.SeriesTimerId = i.RecurringParent.ToString(CultureInfo.InvariantCulture);
        }

        info.Status = ParseStatus(i.Status);
        if (i.File is not null)
        {
            if (Plugin.Instance.Configuration.RecordingTransport == 2)
            {
                info.Url = i.File;
            }
            else
            {
                string? sidParameter = null;
                if (Plugin.Instance.Configuration.RecordingTransport == 1 || Plugin.Instance.Configuration.BackendVersion < 60106)
                {
                    sidParameter = $"&sid={LiveTvService.Instance?.Sid}";
                }

                if (info.Status == RecordingStatus.InProgress)
                {
                    info.Url = $"{_baseUrl}/live?recording={i.Id}{sidParameter}&growing=true";
                }
                else
                {
                    info.Url = $"{_baseUrl}/live?recording={i.Id}{sidParameter}";
                }
            }
        }

        info.StartDate = DateTimeOffset.FromUnixTimeSeconds(i.StartTime).UtcDateTime;
        info.EndDate = DateTimeOffset.FromUnixTimeSeconds(i.StartTime + i.Duration).UtcDateTime;

        info.ProgramId = i.EpgEventId.ToString(CultureInfo.InvariantCulture);
        info.EpisodeTitle = i.Subtitle;
        info.Name = i.Name;
        info.Overview = i.Desc;
        info.IsRepeat = !i.Firstrun;
        info.ChannelId = i.ChannelId.ToString(CultureInfo.InvariantCulture);
        info.ChannelType = ChannelType.TV;
        info.ImageUrl = _baseUrl + "/service?method=channel.show.artwork&prefer=landscape&name=" + Uri.EscapeDataString(i.Name);
        info.HasImage = true;
        if (i.Season.HasValue)
        {
            // NextPVR cannot express specials as season 0, so a zero means there is no season.
            if (i.Season > 0)
            {
                info.SeasonNumber = i.Season;
            }

            info.EpisodeNumber = i.Episode;
            info.IsSeries = true;
            info.EpisodeTitle = GetEpisodeTitle(i);
        }

        if (i.Original is not null)
        {
            info.OriginalAirDate = i.Original;
        }

        info.ProductionYear = i.Year;
        info.OfficialRating = i.Rating;

        if (i.Genres is not null)
        {
            info.Genres = i.Genres;
            genreMapper.PopulateRecordingGenres(info);
        }
        else
        {
            info.Genres = [];
        }

        return info;
    }

    private TimerInfo GetTimerInfo(Recording i)
    {
        var genreMapper = new GenreMapper(Plugin.Instance.Configuration);
        var info = new TimerInfo();
        if (i.Recurring)
        {
            info.SeriesTimerId = i.RecurringParent.ToString(CultureInfo.InvariantCulture);
            info.IsSeries = true;
        }

        info.ChannelId = i.ChannelId.ToString(CultureInfo.InvariantCulture);
        info.Id = i.Id.ToString(CultureInfo.InvariantCulture);
        info.Status = ParseStatus(i.Status);
        info.StartDate = DateTimeOffset.FromUnixTimeSeconds(i.StartTime).UtcDateTime;
        info.EndDate = DateTimeOffset.FromUnixTimeSeconds(i.StartTime + i.Duration).UtcDateTime;
        info.PrePaddingSeconds = i.PrePadding * 60;
        info.PostPaddingSeconds = i.PostPadding * 60;
        info.ProgramId = i.EpgEventId.ToString(CultureInfo.InvariantCulture);
        info.Name = i.Name;
        info.Overview = i.Desc;
        info.EpisodeTitle = i.Subtitle;
        if (i.Season.HasValue)
        {
            // NextPVR cannot express specials as season 0, so a zero means there is no season.
            if (i.Season > 0)
            {
                info.SeasonNumber = i.Season;
            }

            info.EpisodeNumber = i.Episode;
            info.IsSeries = true;
            info.EpisodeTitle = GetEpisodeTitle(i);
        }

        info.OfficialRating = i.Rating;
        if (i.Original is not null)
        {
            info.OriginalAirDate = i.Original;
        }

        info.ProductionYear = i.Year;

        if (i.Genres is not null)
        {
            info.Genres = i.Genres.ToArray();
            genreMapper.PopulateTimerGenres(info);
        }

        info.IsRepeat = !i.Firstrun;
        return info;
    }

    /// <summary>
    /// Gets the title of an episode, with the season and episode prefix that NextPVR puts in
    /// front of the subtitle removed.
    /// </summary>
    /// <param name="recording">The recording to read the title from.</param>
    /// <returns>The episode title, or <c>null</c> when the episode has no title of its own.</returns>
    private static string? GetEpisodeTitle(Recording recording)
    {
        if (recording.Subtitle is null || recording.Season is null || recording.Episode is null)
        {
            return recording.Subtitle;
        }

        string prefix = string.Format(CultureInfo.InvariantCulture, "S{0:D2}E{1:D2}", recording.Season, recording.Episode);
        if (!recording.Subtitle.StartsWith(prefix, StringComparison.Ordinal))
        {
            return recording.Subtitle;
        }

        // NextPVR sends the bare prefix when the episode has no title, and otherwise
        // separates the title from it with " - ".
        string title = recording.Subtitle[prefix.Length..].TrimStart(' ', '-');
        return title.Length == 0 ? null : title;
    }

    private RecordingStatus ParseStatus(string value)
    {
        if (string.Equals(value, "ready", StringComparison.OrdinalIgnoreCase))
        {
            return RecordingStatus.Completed;
        }

        if (string.Equals(value, "recording", StringComparison.OrdinalIgnoreCase))
        {
            return RecordingStatus.InProgress;
        }

        if (string.Equals(value, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return RecordingStatus.Error;
        }

        if (string.Equals(value, "conflict", StringComparison.OrdinalIgnoreCase))
        {
            return RecordingStatus.ConflictedNotOk;
        }

        if (string.Equals(value, "deleted", StringComparison.OrdinalIgnoreCase))
        {
            return RecordingStatus.Cancelled;
        }

        return RecordingStatus.New;
    }

    private sealed class Recording
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Desc { get; set; }

        public string? Subtitle { get; set; }

        public int StartTime { get; set; }

        public int Duration { get; set; }

        public int? Season { get; set; }

        public int? Episode { get; set; }

        public int EpgEventId { get; set; }

        public List<string>? Genres { get; set; }

        public string Status { get; set; } = string.Empty;

        public string Rating { get; set; } = string.Empty;

        public string? Quality { get; set; }

        public string? Channel { get; set; }

        public int ChannelId { get; set; }

        public bool Blue { get; set; }

        public bool Green { get; set; }

        public bool Yellow { get; set; }

        public bool Red { get; set; }

        public int PrePadding { get; set; }

        public int PostPadding { get; set; }

        public string? File { get; set; }

        public int PlaybackPosition { get; set; }

        public bool Played { get; set; }

        public bool Recurring { get; set; }

        public int RecurringParent { get; set; }

        public bool Firstrun { get; set; }

        public string? Reason { get; set; }

        public string? Significance { get; set; }

        public DateTime? Original { get; set; }

        public int? Year { get; set; }
    }

    private sealed class RootObject
    {
        public List<Recording>? Recordings { get; set; }
    }
}
