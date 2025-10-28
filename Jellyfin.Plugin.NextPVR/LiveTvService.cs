using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.NextPVR.Entities;
using Jellyfin.Plugin.NextPVR.Helpers;
using Jellyfin.Plugin.NextPVR.Responses;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.Logging;
using IConfigurationManager = MediaBrowser.Common.Configuration.IConfigurationManager;

namespace Jellyfin.Plugin.NextPVR;

/// <summary>
/// Class LiveTvService.
/// </summary>
public class LiveTvService : ILiveTvService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly bool _enableIPv6;
    private readonly ILogger<LiveTvService> _logger;
    private int _liveStreams;

    private string _baseUrl;

    public LiveTvService(IHttpClientFactory httpClientFactory, ILogger<LiveTvService> logger, IConfigurationManager configuration)
    {
        _enableIPv6 = configuration.GetNetworkConfiguration().EnableIPv6;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        LastUpdatedSidDateTime = DateTime.UtcNow;
        Instance = this;
    }

    public string Sid { get; set; }

    public DateTime RecordingModificationTime { get; set; }

    public static LiveTvService Instance { get; private set; }

    public bool IsActive => Sid is not null;

    public bool FlagRecordingChange { get; set; }

    private DateTimeOffset LastUpdatedSidDateTime { get; set; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name => "Next Pvr";

    public string HomePageUrl => "https://www.nextpvr.com/";

    /// <summary>
    /// Ensure that we are connected to the NextPvr server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<bool> EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance.Configuration;
        {
            if (!Uri.IsWellFormedUriString(config.WebServiceUrl, UriKind.Absolute))
            {
                _logger.LogError("Web service URL must be configured");
                throw new InvalidOperationException("NextPVR web service URL must be configured.");
            }

            if (string.IsNullOrEmpty(config.Pin))
            {
                _logger.LogError("PIN must be configured");
                throw new InvalidOperationException("NextPVR PIN must be configured.");
            }

            if (string.IsNullOrEmpty(config.StoredSid))
            {
                Sid = null;
                LastUpdatedSidDateTime = DateTimeOffset.MinValue;
            }

            if (string.IsNullOrEmpty(Sid) || ((!string.IsNullOrEmpty(Sid)) && (LastUpdatedSidDateTime.AddMinutes(5) < DateTimeOffset.UtcNow)) || RecordingModificationTime != Plugin.Instance.Configuration.RecordingModificationTime)
            {
                try
                {
                    await InitiateSession(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Sid = null;
                    _logger.LogError(ex, "Error initiating session");
                }
            }
        }

        return IsActive;
    }

    /// <summary>
    /// Initiate the nextPvr session.
    /// </summary>
    private async Task InitiateSession(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start InitiateSession");
        _baseUrl = Plugin.Instance.Configuration.CurrentWebServiceURL;
        var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
        httpClient.Timeout = TimeSpan.FromSeconds(5);
        bool updateConfiguration = false;
        bool validConfiguration = false;
        if (!string.IsNullOrEmpty(Plugin.Instance.Configuration.StoredSid) && !string.IsNullOrEmpty(Plugin.Instance.Configuration.CurrentWebServiceURL) )
        {
            string request = $"{_baseUrl}/service?method=session.valid&device=jellyfin&sid={Plugin.Instance.Configuration.StoredSid}";
            await using var stream = await httpClient.GetStreamAsync(request, cancellationToken).ConfigureAwait(false);
            validConfiguration = await new InitializeResponse().LoggedIn(stream, _logger).ConfigureAwait(false);
        }

        if (!validConfiguration)
        {
            UriBuilder builder = new UriBuilder(Plugin.Instance.Configuration.WebServiceUrl);
            if (!_enableIPv6 && builder.Host != "localhost" && builder.Host != "127.0.0.1")
            {
                if (builder.Host == "[::1]")
                {
                    builder.Host = "127.0.0.1";
                }

                try
                {
                    Uri uri = new Uri(Plugin.Instance.Configuration.WebServiceUrl);
                    var hosts = await Dns.GetHostEntryAsync(uri.Host, System.Net.Sockets.AddressFamily.InterNetwork, cancellationToken);
                    if (hosts is not null)
                    {
                        var host = hosts.AddressList.FirstOrDefault()?.ToString();
                        if (builder.Host != host && host is not null)
                        {
                            _logger.LogInformation("Changed host from {OldHost} to {NewHost}", builder.Host, host);
                            builder.Host = host;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not resolve {WebServiceUrl}", Plugin.Instance.Configuration.WebServiceUrl);
                }
            }

            _baseUrl = builder.ToString().TrimEnd('/');
            await using var stream = await httpClient.GetStreamAsync($"{_baseUrl}/service?method=session.initiate&ver=1.0&device=jellyfin", cancellationToken).ConfigureAwait(false);
            var clientKeys = await new InstantiateResponse().GetClientKeys(stream, _logger).ConfigureAwait(false);
            var sid = clientKeys.Sid;
            var salt = clientKeys.Salt;
            validConfiguration = await Login(sid, salt, cancellationToken).ConfigureAwait(false);
            Plugin.Instance.Configuration.StoredSid = sid;
            updateConfiguration = true;
        }

        if (validConfiguration)
        {
            LastUpdatedSidDateTime = DateTimeOffset.UtcNow;
            Sid = Plugin.Instance.Configuration.StoredSid;
            _logger.LogInformation("Session initiated");
            _logger.LogInformation("Sid: {Sid}", Sid);
            if (updateConfiguration)
            {
                Plugin.Instance.Configuration.CurrentWebServiceURL = _baseUrl;
                Plugin.Instance.Configuration.RecordingModificationTime = DateTime.UtcNow;
                Plugin.Instance.SaveConfiguration();
            }

            RecordingModificationTime = Plugin.Instance.Configuration.RecordingModificationTime;

            await GetDefaultSettingsAsync(cancellationToken).ConfigureAwait(false);
            Plugin.Instance.Configuration.GetEpisodeImage = await GetBackendSettingAsync("/Settings/General/ArtworkFromSchedulesDirect", cancellationToken).ConfigureAwait(false) == "true";
        }
        else
        {
            Sid = null;
            _logger.LogError("PIN not accepted");
            throw new UnauthorizedAccessException("NextPVR PIN not accepted");
        }
    }

    private async Task<bool> Login(string sid, string salt, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start Login procedure for Sid: {Sid} & Salt: {Salt}", sid, salt);
        var pin = Plugin.Instance.Configuration.Pin;
        _logger.LogInformation("PIN: {Pin}", pin == "0000" ? pin : "Not default");

        var strb = new StringBuilder();
        var md5Result = GetMd5Hash(strb.Append(':').Append(GetMd5Hash(pin)).Append(':').Append(salt).ToString());

        var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
        await using var stream = await httpClient.GetStreamAsync($"{_baseUrl}/service?method=session.login&md5={md5Result}&sid={sid}", cancellationToken);
        {
            return await new InitializeResponse().LoggedIn(stream, _logger).ConfigureAwait(false);
        }
    }

    private string GetMd5Hash(string value)
    {
#pragma warning disable CA5351
        var hashValue = MD5.HashData(new UTF8Encoding().GetBytes(value));
#pragma warning restore CA5351
        // Bit convertor return the byte to string as all caps hex values separated by "-"
        return Convert.ToHexStringLower(hashValue).Replace("-", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the channels async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task{IEnumerable{ChannelInfo}}.</returns>
    public async Task<IEnumerable<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start GetChannels Async, retrieve all channels");
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetStreamAsync($"{_baseUrl}/service?method=channel.list&sid={Sid}", cancellationToken);

        return await new ChannelResponse(Plugin.Instance.Configuration.WebServiceUrl).GetChannels(stream, _logger).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the Recordings async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task{IEnumerable{RecordingInfo}}.</returns>
    public async Task<IReadOnlyList<MyRecordingInfo>> GetAllRecordingsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start GetRecordings Async, retrieve all 'Pending', 'Inprogress' and 'Completed' recordings ");
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetStreamAsync($"{_baseUrl}/service?method=recording.list&filter=ready&sid={Sid}", cancellationToken);
        return await new RecordingResponse(_baseUrl, _logger).GetRecordings(stream).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete the Recording async from the disk.
    /// </summary>
    /// <param name="recordingId">The recordingId.</param>
    /// <param name="cancellationToken">The cancellationToken.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task DeleteRecordingAsync(string recordingId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start Delete Recording Async for recordingId: {RecordingId}", recordingId);
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetStreamAsync($"{_baseUrl}/service?method=recording.delete&recording_id={recordingId}&sid={Sid}", cancellationToken);

        bool? error = await new CancelDeleteRecordingResponse().RecordingError(stream, _logger).ConfigureAwait(false);

        if (error is null or true)
        {
            _logger.LogError("Failed to delete the recording for recordingId: {RecordingId}", recordingId);
            throw new JsonException($"Failed to delete the recording for recordingId: {recordingId}");
        }
        else
        {
            FlagRecordingChange = true;
        }

        _logger.LogInformation("Deleted Recording with recordingId: {RecordingId}", recordingId);
    }

    /// <summary>
    /// Cancel pending scheduled Recording.
    /// </summary>
    /// <param name="timerId">The timerId.</param>
    /// <param name="cancellationToken">The cancellationToken.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task CancelTimerAsync(string timerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start Cancel Recording Async for recordingId: {TimerId}", timerId);
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetStreamAsync($"{_baseUrl}/service?method=recording.delete&recording_id={timerId}&sid={Sid}", cancellationToken);

        bool? error = await new CancelDeleteRecordingResponse().RecordingError(stream, _logger).ConfigureAwait(false);

        if (error is null or true)
        {
            _logger.LogError("Failed to cancel the recording for recordingId: {TimerId}", timerId);
            throw new JsonException($"Failed to cancel the recording for recordingId: {timerId}");
        }
        else
        {
            FlagRecordingChange = true;
        }

        _logger.LogInformation("Cancelled Recording for recordingId: {TimerId}", timerId);
    }

    /// <summary>
    /// Create a new scheduled recording.
    /// </summary>
    /// <param name="info">The TimerInfo.</param>
    /// <param name="cancellationToken">The cancellationToken.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task CreateTimerAsync(TimerInfo info, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start CreateTimer Async for ChannelId: {ChannelId} & Name: {Name}", info.ChannelId, info.Name);
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        UtilsHelper.DebugInformation(_logger, $"TimerSettings CreateTimer: {info.ProgramId} for ChannelId: {info.ChannelId} & Name: {info.Name}");
        await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetStreamAsync(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}/service?method=recording.save&sid={1}&event_id={2}&pre_padding={3}&post_padding={4}",
                    _baseUrl,
                    Sid,
                    int.Parse(info.ProgramId, CultureInfo.InvariantCulture),
                    info.PrePaddingSeconds / 60,
                    info.PostPaddingSeconds / 60),
                cancellationToken);

        bool? error = await new CancelDeleteRecordingResponse().RecordingError(stream, _logger).ConfigureAwait(false);
        if (error is null or true)
        {
            _logger.LogError("Failed to create the timer with programId: {ProgramId}", info.ProgramId);
            throw new JsonException($"Failed to create the timer with programId: {info.ProgramId}");
        }
        else if (info.StartDate <= DateTime.UtcNow)
        {
            FlagRecordingChange = true;
        }

        _logger.LogError("CreateTimer async for programId: {ProgramId}", info.ProgramId);
    }

    /// <summary>
    /// Get the pending Timers.
    /// </summary>
    /// <param name="cancellationToken">The CancellationToken.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<IEnumerable<TimerInfo>> GetTimersAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start GetTimer Async, retrieve the 'Pending' recordings");
        if (await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false))
        {
            await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
                .GetStreamAsync($"{_baseUrl}/service?method=recording.list&filter=pending&sid={Sid}", cancellationToken);

            return await new RecordingResponse(_baseUrl, _logger).GetTimers(stream).ConfigureAwait(false);
        }

        return new List<TimerInfo>();
    }

    /// <summary>
    /// Get the recurrent recordings.
    /// </summary>
    /// <param name="cancellationToken">The CancellationToken.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<IEnumerable<SeriesTimerInfo>> GetSeriesTimersAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start GetSeriesTimer Async, retrieve the recurring recordings");
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetStreamAsync($"{_baseUrl}/service?method=recording.recurring.list&sid={Sid}", cancellationToken);

        return await new RecurringResponse(_logger).GetSeriesTimers(stream).ConfigureAwait(false);
    }

    /// <summary>
    /// Create a recurrent recording.
    /// </summary>
    /// <param name="info">The recurring program info.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task CreateSeriesTimerAsync(SeriesTimerInfo info, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start CreateSeriesTimer Async for ChannelId: {ChannelId} & Name: {Name}", info.ChannelId, info.Name);
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        var url = $"{_baseUrl}/service?method=recording.recurring.save&sid={Sid}&pre_padding={info.PrePaddingSeconds / 60}&post_padding={info.PostPaddingSeconds / 60}&keep={info.KeepUpTo}";

        int recurringType = int.Parse(Plugin.Instance.Configuration.RecordingDefault, CultureInfo.InvariantCulture);

        if (recurringType == 99)
        {
            url += string.Format(CultureInfo.InvariantCulture, "&name={0}&keyword=title+like+'{0}'", Uri.EscapeDataString(info.Name.Replace("'", "''", StringComparison.Ordinal)));
        }
        else
        {
            url += $"&event_id={info.ProgramId}&recurring_type={recurringType}";
        }

        if (info.RecordNewOnly || Plugin.Instance.Configuration.NewEpisodes)
        {
            url += "&only_new=true";
        }

        if (recurringType is 3 or 4)
        {
            url += "&timeslot=true";
        }

        await CreateUpdateSeriesTimerAsync(info, url, cancellationToken);
    }

    private async Task CreateUpdateSeriesTimerAsync(SeriesTimerInfo info, string url, CancellationToken cancellationToken)
    {
        UtilsHelper.DebugInformation(_logger, $"TimerSettings CreateSeriesTimerAsync: {info.ProgramId} for ChannelId: {info.ChannelId} & Name: {info.Name}");
        await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetStreamAsync(url, cancellationToken);

        bool? error = await new CancelDeleteRecordingResponse().RecordingError(stream, _logger).ConfigureAwait(false);
        if (error is null or true)
        {
            _logger.LogError("Failed to create or update the timer with Recurring ID: {TimerInfoId}", info.Id);
            throw new JsonException($"Failed to create or update the timer with Recurring ID: {info.Id}");
        }

        _logger.LogInformation("CreateUpdateSeriesTimer async for Program ID: {ProgramId} Recurring ID {TimerInfoId}", info.ProgramId, info.Id);
    }

    /// <summary>
    /// Update the series Timer.
    /// </summary>
    /// <param name="info">The series program info.</param>
    /// <param name="cancellationToken">The CancellationToken.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task UpdateSeriesTimerAsync(SeriesTimerInfo info, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start UpdateSeriesTimer Async for ChannelId: {ChannelId} & Name: {Name}", info.ChannelId, info.Name);
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        var url = $"{_baseUrl}/service?method=recording.recurring.save&sid={Sid}&pre_padding={info.PrePaddingSeconds / 60}&post_padding={info.PostPaddingSeconds / 60}&keep={info.KeepUpTo}&recurring_id={info.Id}";

        int recurringType = 2;

        if (info.RecordAnyChannel)
        {
            url += string.Format(CultureInfo.InvariantCulture, "&name={0}&keyword=title+like+'{0}'", Uri.EscapeDataString(info.Name.Replace("'", "''", StringComparison.Ordinal)));
        }
        else
        {
            if (info.RecordAnyTime)
            {
                if (info.RecordNewOnly)
                {
                    recurringType = 1;
                }
            }
            else
            {
                recurringType = info.Days.Count == 7 ? 4 : 3;
            }

            url += $"&recurring_type={recurringType}";
        }

        if (info.RecordNewOnly)
        {
            url += "&only_new=true";
        }

        await CreateUpdateSeriesTimerAsync(info, url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Update a single Timer.
    /// </summary>
    /// <param name="updatedTimer">The program info.</param>
    /// <param name="cancellationToken">The CancellationToken.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task UpdateTimerAsync(TimerInfo updatedTimer, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start UpdateTimer Async for ChannelId: {ChannelId} & Name: {Name}", updatedTimer.ChannelId, updatedTimer.Name);
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetStreamAsync($"{_baseUrl}/service?method=recording.save&sid={Sid}&pre_padding={updatedTimer.PrePaddingSeconds / 60}&post_padding={updatedTimer.PostPaddingSeconds / 60}&recording_id={updatedTimer.Id}&event_id={updatedTimer.ProgramId}", cancellationToken);

        bool? error = await new CancelDeleteRecordingResponse().RecordingError(stream, _logger).ConfigureAwait(false);
        if (error is null or true)
        {
            _logger.LogError("Failed to update the timer with ID: {Id}", updatedTimer.Id);
            throw new JsonException($"Failed to update the timer with ID: {updatedTimer.Id}");
        }

        _logger.LogInformation("UpdateTimer async for Program ID: {ProgramId} ID {Id}", updatedTimer.ProgramId, updatedTimer.Id);
    }

    /// <summary>
    /// Cancel the Series Timer.
    /// </summary>
    /// <param name="timerId">The Timer Id.</param>
    /// <param name="cancellationToken">The CancellationToken.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task CancelSeriesTimerAsync(string timerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start Cancel SeriesRecording Async for recordingId: {TimerId}", timerId);
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetStreamAsync($"{_baseUrl}/service?method=recording.recurring.delete&recurring_id={timerId}&sid={Sid}", cancellationToken);

        bool? error = await new CancelDeleteRecordingResponse().RecordingError(stream, _logger).ConfigureAwait(false);

        if (error is null or true)
        {
            _logger.LogError("Failed to cancel the recording with recordingId: {TimerId}", timerId);
            throw new JsonException($"Failed to cancel the recording with recordingId: {timerId}");
        }

        _logger.LogInformation("Cancelled Recording for recordingId: {TimerId}", timerId);
    }

    public async Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(string channelId, CancellationToken cancellationToken)
    {
        var source = await GetChannelStream(channelId, string.Empty, cancellationToken);
        return [source];
    }

    public Task<MediaSourceInfo> GetChannelStream(string channelId, string streamId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start ChannelStream");
        EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        _liveStreams++;

        string sidParameter = null;
        if (Plugin.Instance.Configuration.RecordingTransport != 3)
        {
            sidParameter = $"&sid={Sid}";
        }

        string streamUrl = $"{_baseUrl}/live?channeloid={channelId}&client=jellyfin.{_liveStreams.ToString(CultureInfo.InvariantCulture)}{sidParameter}";
        _logger.LogInformation("Streaming {Url}", streamUrl);
        var mediaSourceInfo = new MediaSourceInfo
        {
            Id = _liveStreams.ToString(CultureInfo.InvariantCulture),
            Path = streamUrl,
            Protocol = MediaProtocol.Http,
            RequiresOpening = true,
            MediaStreams = new List<MediaStream>
            {
                new MediaStream
                {
                    Type = MediaStreamType.Video,
                    // IsInterlaced = true,
                    // Set the index to -1 because we don't know the exact index of the video stream within the container
                    Index = -1,
                },
                new MediaStream
                {
                    Type = MediaStreamType.Audio,
                    // Set the index to -1 because we don't know the exact index of the audio stream within the container
                    Index = -1
                }
            },
            Container = "mpegts",
            SupportsProbing = true
        };

        return Task.FromResult(mediaSourceInfo);
    }

    public Task CloseLiveStream(string id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Closing {Id}", id);
        return Task.CompletedTask;
    }

    public Task<SeriesTimerInfo> GetNewTimerDefaultsAsync(CancellationToken cancellationToken, ProgramInfo program = null)
    {
        SeriesTimerInfo defaultSettings = new SeriesTimerInfo
        {
            PrePaddingSeconds = Plugin.Instance.Configuration.PrePaddingSeconds,
            PostPaddingSeconds = Plugin.Instance.Configuration.PostPaddingSeconds
        };
        return Task.FromResult(defaultSettings);
    }

    private async Task GetDefaultSettingsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start GetDefaultSettings Async");
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetStreamAsync($"{_baseUrl}/service?method=setting.list&sid={Sid}", cancellationToken);
        await new SettingResponse().GetDefaultSettings(stream, _logger).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(string channelId, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start GetPrograms Async, retrieve all Programs");
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        string fastArt = Plugin.Instance.Configuration.BackendVersion >= 70003 ? "&fastart" : string.Empty;
        await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetStreamAsync($"{_baseUrl}/service?method=channel.listings&sid={Sid}&start={((DateTimeOffset)startDateUtc).ToUnixTimeSeconds()}&end={((DateTimeOffset)endDateUtc).ToUnixTimeSeconds()}&channel_id={channelId}{fastArt}", cancellationToken);
        return await new ListingsResponse(_baseUrl).GetPrograms(stream, channelId, _logger).ConfigureAwait(false);
    }

    public async Task<DateTimeOffset> GetLastUpdate(CancellationToken cancellationToken)
    {
        _logger.LogDebug("GetLastUpdateTime");
        DateTimeOffset retTime = DateTimeOffset.FromUnixTimeSeconds(0);

        try
        {
            var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            var stream = await httpClient.GetStreamAsync($"{_baseUrl}/service?method=recording.lastupdated&ignore_resume=true&sid={Sid}", cancellationToken);
            retTime = await new LastUpdateResponse().GetUpdateTime(stream, _logger).ConfigureAwait(false);
            if (retTime == DateTimeOffset.FromUnixTimeSeconds(0))
            {
                LastUpdatedSidDateTime = DateTimeOffset.MinValue;
            }
            else if (LastUpdatedSidDateTime != DateTimeOffset.MinValue)
            {
                LastUpdatedSidDateTime = DateTimeOffset.UtcNow;
            }

            UtilsHelper.DebugInformation(_logger, $"GetLastUpdateTime {retTime.ToUnixTimeSeconds()}");
        }
        catch (Exception ex)
        {
            LastUpdatedSidDateTime = DateTimeOffset.MinValue;
            _logger.LogWarning(ex, "Could not connect to server");
            Sid = null;
        }

        return retTime;
    }

    private async Task<string> GetBackendSettingAsync(string key, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetBackendSetting");
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetStreamAsync($"{_baseUrl}/service?method=setting.get&key={key}&sid={Sid}", cancellationToken);

        return await new SettingResponse().GetSetting(stream, _logger).ConfigureAwait(false);
    }

    public Task ResetTuner(string id, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
