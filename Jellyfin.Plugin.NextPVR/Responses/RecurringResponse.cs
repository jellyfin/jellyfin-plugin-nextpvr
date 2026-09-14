using System;
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

internal sealed class RecurringResponse
{
    private static readonly (string Code, DayOfWeek Day)[] DayCodes =
    [
        ("SUN", DayOfWeek.Sunday),
        ("MON", DayOfWeek.Monday),
        ("TUE", DayOfWeek.Tuesday),
        ("WED", DayOfWeek.Wednesday),
        ("THU", DayOfWeek.Thursday),
        ("FRI", DayOfWeek.Friday),
        ("SAT", DayOfWeek.Saturday)
    ];

    private readonly ILogger<LiveTvService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    public RecurringResponse(ILogger<LiveTvService> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<SeriesTimerInfo>> GetSeriesTimers(Stream stream)
    {
        if (stream is null)
        {
            _logger.LogError("GetSeriesTimers stream is null");
            throw new ArgumentNullException(nameof(stream));
        }

        var root = await JsonSerializer.DeserializeAsync<RecurringRoot>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(_logger, $"GetSeriesTimers Response: {JsonSerializer.Serialize(root, _jsonOptions)}");

        if (root?.Recurrings is null)
        {
            _logger.LogError("Failed to download the recurring recordings");
            throw new JsonException("Failed to download the recurring recordings.");
        }

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
            StartDate = DateTimeOffset.FromUnixTimeSeconds(i.StartTimeTicks).UtcDateTime,
            EndDate = DateTimeOffset.FromUnixTimeSeconds(i.EndTimeTicks).UtcDateTime,
            PrePaddingSeconds = i.PrePadding * 60,
            PostPaddingSeconds = i.PostPadding * 60,
            Name = i.Name ?? i.EpgTitle,
            RecordNewOnly = i.OnlyNewEpisodes
        };

        if (info.ChannelId == "0")
        {
            info.RecordAnyChannel = true;
        }

        if (i.Days is null)
        {
            info.RecordAnyTime = true;
        }
        else
        {
            var days = ParseDays(i.Days);
            if (days.Count == 0)
            {
                _logger.LogWarning("Unrecognized day mask {Days} on recurring recording {RecurringId}", i.Days, i.Id);
                info.RecordAnyTime = true;
            }
            else
            {
                info.Days = days;
            }
        }

        return info;
    }

    /// <summary>
    /// Parses the day mask of a recurring recording.
    /// </summary>
    /// <param name="days">
    /// The day mask, either the alias "WEEKENDS" or "WEEKDAYS", or the three letter codes of the
    /// individual days separated by colons, such as "SAT:SUN:".
    /// </param>
    /// <returns>The days the recurring recording runs on, empty if the mask was not recognized.</returns>
    private static List<DayOfWeek> ParseDays(string days)
    {
        if (days.Contains("WEEKENDS", StringComparison.OrdinalIgnoreCase))
        {
            return [DayOfWeek.Saturday, DayOfWeek.Sunday];
        }

        if (days.Contains("WEEKDAYS", StringComparison.OrdinalIgnoreCase))
        {
            return [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
        }

        List<DayOfWeek> parsed = [];
        foreach ((string code, DayOfWeek day) in DayCodes)
        {
            if (days.Contains(code, StringComparison.OrdinalIgnoreCase))
            {
                parsed.Add(day);
            }
        }

        return parsed;
    }
}
