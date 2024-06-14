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

internal sealed class RecurringResponse
{
    private readonly ILogger<LiveTvService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    public RecurringResponse(ILogger<LiveTvService> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<SeriesTimerInfo>> GetSeriesTimers(Stream stream)
    {
        if (stream == null)
        {
            _logger.LogError("GetSeriesTimers stream == null");
            throw new ArgumentNullException(nameof(stream));
        }

        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(_logger, $"GetSeriesTimers Response: {JsonSerializer.Serialize(root, _jsonOptions)}");
        return root.Recurrings
            .Select(i => i)
            .Select(GetSeriesTimerInfo);
    }

    private SeriesTimerInfo GetSeriesTimerInfo(Recurring i)
    {
        var info = new SeriesTimerInfo
        {
            ChannelId = i.ChannelId.ToString(CultureInfo.InvariantCulture),
            Id = i.Id.ToString(CultureInfo.InvariantCulture),
            StartDate = DateTimeOffset.FromUnixTimeSeconds(i.StartTimeTicks).DateTime,
            EndDate = DateTimeOffset.FromUnixTimeSeconds(i.EndTimeTicks).DateTime,
            PrePaddingSeconds = i.PrePadding * 60,
            PostPaddingSeconds = i.PostPadding * 60,
            Name = i.Name ?? i.EpgTitle,
            RecordNewOnly = i.OnlyNewEpisodes
        };

        if (info.ChannelId == "0")
        {
            info.RecordAnyChannel = true;
        }

        if (i.Days == null)
        {
            info.RecordAnyTime = true;
        }
        else
        {
            info.Days = (i.Days ?? string.Empty).Split(':')
                .Select(d => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), d.Trim(), true))
                .ToList();
        }

        return info;
    }

    private sealed class Recurring
    {
        public int Id { get; set; }

        public int Type { get; set; }

        public string Name { get; set; }

        public int ChannelId { get; set; }

        public string Channel { get; set; }

        public string Period { get; set; }

        public int Keep { get; set; }

        public int PrePadding { get; set; }

        public int PostPadding { get; set; }

        public string EpgTitle { get; set; }

        public string DirectoryId { get; set; }

        public string Days { get; set; }

        public bool Enabled { get; set; }

        public bool OnlyNewEpisodes { get; set; }

        public int StartTimeTicks { get; set; }

        public int EndTimeTicks { get; set; }

        public string AdvancedRules { get; set; }
    }

    private sealed class RootObject
    {
        public List<Recurring> Recurrings { get; set; }
    }
}
