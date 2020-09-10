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
using NextPvr.Helpers;


namespace NextPvr.Responses
{
    class RecurringResponse
    {
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");
        private readonly string _baseUrl;
        private IFileSystem _fileSystem;

        public RecurringResponse(string baseUrl, IFileSystem fileSystem)
        {
            _baseUrl = baseUrl;
            _fileSystem = fileSystem;
        }
        public IEnumerable<SeriesTimerInfo> GetSeriesTimers(Stream stream, IJsonSerializer json, ILogger logger)
        {
            if (stream == null)
            {
                logger.LogError("[NextPVR] GetSeriesTimers stream == null");
                throw new ArgumentNullException("stream");
            }

            var root = json.DeserializeFromStream<RootObject>(stream);
            UtilsHelper.DebugInformation(logger, string.Format("[NextPVR] GetSeriesTimers Response: {0}", json.SerializeToString(root)));
            return root.recurrings
                .Select(i => i)
                .Select(GetSeriesTimerInfo);
        }
        private SeriesTimerInfo GetSeriesTimerInfo(Recurring i)
        {
            var info = new SeriesTimerInfo();
            try
            {
                info.ChannelId = i.channelID.ToString();

                info.Id = i.id.ToString(_usCulture);

                info.StartDate = DateTimeOffset.FromUnixTimeSeconds(i.startTimeTicks).DateTime;
                info.EndDate = DateTimeOffset.FromUnixTimeSeconds(i.endTimeTicks).DateTime;

                info.PrePaddingSeconds = i.prePadding * 60;
                info.PostPaddingSeconds = i.postPadding * 60;

                info.Name = i.name ?? i.epgTitle;
                info.RecordNewOnly = i.onlyNewEpisodes;
                if (info.ChannelId == "0")
                    info.RecordAnyChannel = true;

                if (i.days == null)
                {
                    info.RecordAnyTime = true;
                }
                else
                {
                    info.Days = (i.days ?? string.Empty).Split(':')
                        .Select(d => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), d.Trim(), true))
                        .ToList();
                }

                return info;
            }
            catch (Exception err)
            {
                System.Diagnostics.Debug.WriteLine(err.Message);
                throw (err);
            }
        }        
        private class Recurring
        {
            public int id { get; set; }
            public int type { get; set; }
            public string name { get; set; }
            public int channelID { get; set; }
            public string channel { get; set; }
            public string period { get; set; }
            public int keep { get; set; }
            public int prePadding { get; set; }
            public int postPadding { get; set; }
            public string epgTitle { get; set; }
            public string directoryID { get; set; }
            public string days { get; set; }
            public bool enabled { get; set; }
            public bool onlyNewEpisodes { get; set; }
            public int startTimeTicks { get; set; }
            public int endTimeTicks { get; set; }
            public string advancedRules { get; set; }
        }

        private class RootObject
        {
            public List<Recurring> recurrings { get; set; }
        }
    }
}
