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

public class RecordingResponse
{
    private readonly string _baseUrl;
    private readonly ILogger<LiveTvService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    public RecordingResponse(string baseUrl, ILogger<LiveTvService> logger)
    {
        _baseUrl = baseUrl;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MyRecordingInfo>> GetRecordings(Stream stream)
    {
        if (stream == null)
        {
            _logger.LogError("GetRecording stream == null");
            throw new ArgumentNullException(nameof(stream));
        }

        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(_logger, $"GetRecordings Response: {JsonSerializer.Serialize(root, _jsonOptions)}");

        IEnumerable<MyRecordingInfo> recordings;
        try
        {
            recordings = root.Recordings
                .Select(i => i)
                .Where(i => i.Status != "failed" && i.Status != "conflict")
                .Select(GetRecordingInfo);
        }
        catch (Exception err)
        {
            _logger.LogWarning(err, "Get recordings");
            throw;
        }

        return recordings.ToList();
    }

    public async Task<IEnumerable<TimerInfo>> GetTimers(Stream stream)
    {
        if (stream == null)
        {
            _logger.LogError("GetTimers stream == null");
            throw new ArgumentNullException(nameof(stream));
        }

        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(_logger, $"GetTimers Response: {JsonSerializer.Serialize(root, _jsonOptions)}");
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
        var info = new MyRecordingInfo();
        info.Id = i.Id.ToString(CultureInfo.InvariantCulture);
        if (i.Recurring)
        {
            info.SeriesTimerId = i.RecurringParent.ToString(CultureInfo.InvariantCulture);
        }

        info.Status = ParseStatus(i.Status);
        if (i.File != null)
        {
            if (Plugin.Instance.Configuration.RecordingTransport == 2)
            {
                info.Url = i.File;
            }
            else
            {
                string sidParameter = null;
                if (Plugin.Instance.Configuration.RecordingTransport == 1 || Plugin.Instance.Configuration.BackendVersion < 60106)
                {
                    sidParameter = $"&sid={LiveTvService.Instance.Sid}";
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

        info.StartDate = DateTimeOffset.FromUnixTimeSeconds(i.StartTime).DateTime;
        info.EndDate = DateTimeOffset.FromUnixTimeSeconds(i.StartTime + i.Duration).DateTime;

        info.ProgramId = i.EpgEventId.ToString(CultureInfo.InvariantCulture);
        info.EpisodeTitle = i.Subtitle;
        info.Name = i.Name;
        info.Overview = i.Desc;
        info.Genres = i.Genres;
        info.IsRepeat = !i.Firstrun;
        info.ChannelId = i.ChannelId.ToString(CultureInfo.InvariantCulture);
        info.ChannelType = ChannelType.TV;
        info.ImageUrl = _baseUrl + "/service?method=channel.show.artwork&prefer=landscape&name=" + Uri.EscapeDataString(i.Name);
        info.HasImage = true;
        if (i.Season.HasValue)
        {
            info.SeasonNumber = i.Season;
            info.EpisodeNumber = i.Episode;
            info.IsSeries = true;
            string se = string.Format(CultureInfo.InvariantCulture, "S{0:D2}E{1:D2} - ", i.Season, i.Episode);
            if (i.Subtitle.StartsWith(se, StringComparison.CurrentCulture))
            {
                info.EpisodeTitle = i.Subtitle.Substring(se.Length);
            }
        }

        if (i.Original != null)
        {
            info.OriginalAirDate = i.Original;
        }

        info.ProductionYear = i.Year;
        info.OfficialRating = i.Rating;

        if (info.Genres != null)
        {
            info.Genres = i.Genres;
            genreMapper.PopulateRecordingGenres(info);
        }
        else
        {
            info.Genres = new List<string>();
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
        info.StartDate = DateTimeOffset.FromUnixTimeSeconds(i.StartTime).DateTime;
        info.EndDate = DateTimeOffset.FromUnixTimeSeconds(i.StartTime + i.Duration).DateTime;
        info.PrePaddingSeconds = i.PrePadding * 60;
        info.PostPaddingSeconds = i.PostPadding * 60;
        info.ProgramId = i.EpgEventId.ToString(CultureInfo.InvariantCulture);
        info.Name = i.Name;
        info.Overview = i.Desc;
        info.EpisodeTitle = i.Subtitle;
        if (i.Season.HasValue)
        {
            info.SeasonNumber = i.Season;
            info.EpisodeNumber = i.Episode;
            info.IsSeries = true;
            string se = string.Format(CultureInfo.InvariantCulture, "S{0:D2}E{1:D2} - ", i.Season, i.Episode);
            if (i.Subtitle.StartsWith(se, StringComparison.CurrentCulture))
            {
                info.EpisodeTitle = i.Subtitle.Substring(se.Length);
            }
        }

        info.OfficialRating = i.Rating;
        if (i.Original != null)
        {
            info.OriginalAirDate = i.Original;
        }

        info.ProductionYear = i.Year;

        if (i.Genres != null)
        {
            info.Genres = i.Genres.ToArray();
            genreMapper.PopulateTimerGenres(info);
        }

        info.IsRepeat = !i.Firstrun;
        return info;
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

        public string Name { get; set; }

        public string Desc { get; set; }

        public string Subtitle { get; set; }

        public int StartTime { get; set; }

        public int Duration { get; set; }

        public int? Season { get; set; }

        public int? Episode { get; set; }

        public int EpgEventId { get; set; }

        public List<string> Genres { get; set; }

        public string Status { get; set; }

        public string Rating { get; set; }

        public string Quality { get; set; }

        public string Channel { get; set; }

        public int ChannelId { get; set; }

        public bool Blue { get; set; }

        public bool Green { get; set; }

        public bool Yellow { get; set; }

        public bool Red { get; set; }

        public int PrePadding { get; set; }

        public int PostPadding { get; set; }

        public string File { get; set; }

        public int PlaybackPosition { get; set; }

        public bool Played { get; set; }

        public bool Recurring { get; set; }

        public int RecurringParent { get; set; }

        public bool Firstrun { get; set; }

        public string Reason { get; set; }

        public string Significance { get; set; }

        public DateTime? Original { get; set; }

        public int? Year { get; set; }
    }

    private sealed class RootObject
    {
        public List<Recording> Recordings { get; set; }
    }
}
