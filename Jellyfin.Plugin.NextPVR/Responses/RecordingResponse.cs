using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.LiveTv;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.NextPVR.Helpers;

namespace Jellyfin.Plugin.NextPVR.Responses
{
    public class RecordingResponse
    {
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");
        private readonly string _baseUrl;
        private IFileSystem _fileSystem;
        private readonly ILogger<LiveTvService> _logger;

        public RecordingResponse(string baseUrl, IFileSystem fileSystem, ILogger<LiveTvService> logger)
        {
            _baseUrl = baseUrl;
            _fileSystem = fileSystem;
            _logger = logger;
        }

        public IEnumerable<MyRecordingInfo> GetRecordings(Stream stream, IJsonSerializer json)
        {
            if (stream == null)
            {
                _logger.LogError("[NextPVR] GetRecording stream == null");
                throw new ArgumentNullException("stream");
            }

            var root = json.DeserializeFromStream<RootObject>(stream);
            UtilsHelper.DebugInformation(_logger, string.Format("[NextPVR] GetRecordings Response: {0}", json.SerializeToString(root)));

            IEnumerable<MyRecordingInfo> Recordings;
            try
            {
                Recordings = root.recordings
                    .Select(i => i)
                    .Where(i => i.status != "failed" && i.status != "conflict")
                    .Select(GetRecordingInfo);
            }
            catch (Exception err)
            {
                _logger.LogDebug(err.Message);
                throw err;
            }
            return Recordings;
        }

        public IEnumerable<TimerInfo> GetTimers(Stream stream, IJsonSerializer json)
        {
            if (stream == null)
            {
                _logger.LogError("[NextPVR] GetTimers stream == null");
                throw new ArgumentNullException("stream");
            }

            var root = json.DeserializeFromStream<RootObject>(stream);
            UtilsHelper.DebugInformation(_logger, string.Format("[NextPVR] GetTimers Response: {0}", json.SerializeToString(root)));
            IEnumerable<TimerInfo> Timers;
            try
            {
                Timers = root.recordings
                    .Select(i => i)
                    .Select(GetTimerInfo);

            }
            catch (Exception err)
            {
                _logger.LogDebug(err.Message);
                throw err;
            }
            return Timers;
        }
        private MyRecordingInfo GetRecordingInfo(Recording i)
        {
            var genreMapper = new GenreMapper(Plugin.Instance.Configuration);
            var info = new MyRecordingInfo();
            try
            {
                info.Id = i.id.ToString(_usCulture);
                if (i.recurring)
                {
                    info.SeriesTimerId = i.recurringParent.ToString(_usCulture);
                }

                if (i.file != null)
                {
                    if (Plugin.Instance.Configuration.RecordingTransport == 2)
                    {
                        info.Url = i.file;
                    }
                    else
                    {
                        info.Url = String.Format("{0}/live?recording={1}", _baseUrl, i.id);
                    }
                }

                info.Status = ParseStatus(i.status);
                info.StartDate = DateTimeOffset.FromUnixTimeSeconds(i.startTime).DateTime;
                info.EndDate = DateTimeOffset.FromUnixTimeSeconds(i.startTime + i.duration).DateTime;

                info.ProgramId = i.epgEventId.ToString(_usCulture);
                info.EpisodeTitle = i.subtitle;
                info.Name = i.name;
                info.Overview = i.desc;
                info.Genres = i.genres;
                info.IsRepeat = !i.firstrun;
                info.ChannelId = i.channelId.ToString(_usCulture);
                info.ChannelType = ChannelType.TV;
                info.ImageUrl = _baseUrl + "/service?method=channel.show.artwork&prefer=landscape&name=" + Uri.EscapeDataString(i.name);
                info.HasImage = true;
                if (i.season.HasValue)
                {
                    info.SeasonNumber = i.season;
                    info.EpisodeNumber = i.episode;
                    info.IsSeries = true;    //!string.IsNullOrEmpty(epg.Subtitle); http://emby.media/community/index.php?/topic/21264-series-record-ability-missing-in-emby-epg/#entry239633
                }
                if (i.original != null)
                {
                    info.OriginalAirDate = i.original;
                }
                info.ProductionYear = i.year;
                //info.CommunityRating = ListingsResponse.ParseCommunityRating(epg.StarRating);
                //info.IsHD = true;
                //info.Audio = ProgramAudio.Stereo;

                info.OfficialRating = i.rating;

                if (info.Genres != null)
                {
                    info.Genres = i.genres;
                    genreMapper.PopulateRecordingGenres(info);
                }
                else
                {
                    info.Genres = new List<string>();
                }
                return info;
            }
            catch (Exception err)
            {
                throw (err);
            }
        }

        private TimerInfo GetTimerInfo(Recording i)
        {
            var genreMapper = new GenreMapper(Plugin.Instance.Configuration);
            var info = new TimerInfo();
            try
            {
                if (i.recurring)
                {
                    info.SeriesTimerId = i.recurringParent.ToString(_usCulture);
                    info.IsSeries = true;    //!string.IsNullOrEmpty(epg.Subtitle); http://emby.media/community/index.php?/topic/21264-series-record-ability-missing-in-emby-epg/#entry239633
                }
                info.ChannelId = i.channelId.ToString(_usCulture);
                info.Id = i.id.ToString(_usCulture);
                info.Status = ParseStatus(i.status);
                info.StartDate = DateTimeOffset.FromUnixTimeSeconds(i.startTime).DateTime;
                info.EndDate = DateTimeOffset.FromUnixTimeSeconds(i.startTime + i.duration).DateTime;
                info.PrePaddingSeconds = i.prePadding * 60;
                info.PostPaddingSeconds = i.postPadding * 60;
                info.ProgramId = i.epgEventId.ToString(_usCulture);
                info.Name = i.name;
                info.Overview = i.desc;
                info.EpisodeTitle = i.subtitle;
                info.SeasonNumber = i.season;
                info.EpisodeNumber = i.episode;
                info.OfficialRating = i.rating;
                if (i.original != null)
                {
                    info.OriginalAirDate = i.original;
                }
                info.ProductionYear = i.year;

                if (i.genres != null)
                {
                    info.Genres = i.genres.ToArray();
                    genreMapper.PopulateTimerGenres(info);
                }

                info.IsRepeat = !i.firstrun;

                //info.CommunityRating = ListingsResponse.ParseCommunityRating(epg.StarRating);
                return info;
            }
            catch (Exception err)
            {
                throw (err);
            }
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
        private class Recording
        {
            public int id { get; set; }
            public string name { get; set; }
            public string desc { get; set; }
            public string subtitle { get; set; }
            public int startTime { get; set; }
            public int duration { get; set; }
            public int? season { get; set; }
            public int? episode { get; set; }
            public int epgEventId { get; set; }
            public List<string> genres { get; set; }
            public string status { get; set; }
            public string rating { get; set; }
            public string quality { get; set; }
            public string channel { get; set; }
            public int channelId { get; set; }
            public bool blue { get; set; }
            public bool green { get; set; }
            public bool yellow { get; set; }
            public bool red { get; set; }
            public int prePadding { get; set; }
            public int postPadding { get; set; }
            public string file { get; set; }
            public int playbackPosition { get; set; }
            public bool played { get; set; }
            public bool recurring { get; set; }
            public int recurringParent { get; set; }
            public bool firstrun { get; set; }
            public string reason { get; set; }
            public string significance { get; set; }
            public DateTime? original { get; set; }
            public int? year { get; set; }
        }

        private class RootObject
        {
            public List<Recording> recordings { get; set; }
        }
    }
}
