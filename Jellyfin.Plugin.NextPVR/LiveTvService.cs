using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;
using NextPvr.Helpers;
using NextPvr.Responses;
using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Cryptography;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.LiveTv;

namespace NextPvr
{
    /// <summary>
    /// Class LiveTvService
    /// </summary>
    public class LiveTvService : ILiveTvService
    {
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");
        private readonly ILogger<LiveTvService> _logger;
        private int _liveStreams;
        private readonly Dictionary<int, int> _heartBeat = new Dictionary<int, int>();

        private string Sid { get; set; }
        private DateTimeOffset LastUpdatedSidDateTime { get; set; }
        private IFileSystem _fileSystem;

        public DateTimeOffset LastRecordingChange = DateTimeOffset.MinValue;

        public LiveTvService(IHttpClient httpClient, IJsonSerializer jsonSerializer, ILogger<LiveTvService> logger, ICryptoProvider cryptoProvider, IFileSystem fileSystem)
        {
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
            _logger = logger;
            LastUpdatedSidDateTime = DateTime.UtcNow;
            _fileSystem = fileSystem;
        }

        /// <summary>
        /// Ensure that we are connected to the NextPvr server
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
        {
            var config = Plugin.Instance.Configuration;

            if (string.IsNullOrEmpty(config.WebServiceUrl))
            {
                _logger.LogError("[NextPVR] Web service url must be configured.");
                throw new InvalidOperationException("NextPvr web service url must be configured.");
            }

            if (string.IsNullOrEmpty(config.Pin))
            {
                _logger.LogError("[NextPVR] Pin must be configured.");
                throw new InvalidOperationException("NextPvr pin must be configured.");
            }

            if ((string.IsNullOrEmpty(Sid)) || ((!string.IsNullOrEmpty(Sid)) && (LastUpdatedSidDateTime.AddMinutes(5) < DateTime.UtcNow)))
            {
                await InitiateSession(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Initiate the nextPvr session
        /// </summary>
        private async Task InitiateSession(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPVR] Start InitiateSession");
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/Util/NPVR/Client/Instantiate", baseUrl)
            };

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                var clientKeys = new InstantiateResponse().GetClientKeys(stream, _jsonSerializer, _logger);

                var sid = clientKeys.sid;
                var salt = clientKeys.salt;
                _logger.LogInformation(string.Format("[NextPVR] Sid: {0}", sid));

                var loggedIn = await Login(sid, salt, cancellationToken).ConfigureAwait(false);

                if (loggedIn)
                {
                    _logger.LogInformation("[NextPVR] Session initiated.");
                    Sid = sid;
                    LastUpdatedSidDateTime = DateTimeOffset.UtcNow;
                }
            }
        }

        /// <summary>
        /// Initialize the NextPvr session
        /// </summary>
        /// <param name="sid"></param>
        /// <param name="salt"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<bool> Login(string sid, string salt, CancellationToken cancellationToken)
        {
            _logger.LogInformation(string.Format("[NextPVR] Start Login procedure for Sid: {0} & Salt: {1}", sid, salt));
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;
            var pin = Plugin.Instance.Configuration.Pin;
            _logger.LogInformation(string.Format("[NextPVR] Pin: {0}", pin));

            var strb = new StringBuilder();
            var md5Result = GetMd5Hash(strb.Append(":").Append(GetMd5Hash(pin)).Append(":").Append(salt).ToString());

            var options = new HttpRequestOptions
            {
                Url = string.Format("{0}/public/Util/NPVR/Client/Initialize/{1}?sid={2}", baseUrl, md5Result, sid),
                CancellationToken = cancellationToken
            };

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                return new InitializeResponse().LoggedIn(stream, _jsonSerializer, _logger);
            }
        }

        public string GetMd5Hash(string value)
        {
            return value.GetMD5().ToString();
        }

        /// <summary>
        /// Gets the channels async.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{IEnumerable{ChannelInfo}}.</returns>
        public async Task<IEnumerable<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPVR] Start GetChannels Async, retrieve all channels");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/GuideService/Channels?sid={1}", baseUrl, Sid)
            };

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                return new ChannelResponse(Plugin.Instance.Configuration.WebServiceUrl).GetChannels(stream, _jsonSerializer, _logger).ToList();
            }
        }

        public async Task<IEnumerable<RecordingInfo>> GetRecordingsAsync(CancellationToken cancellationToken)
        {
            return new List<RecordingInfo>();
        }

        /// <summary>
        /// Gets the Recordings async
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{IEnumerable{RecordingInfo}}</returns>
        public async Task<IEnumerable<MyRecordingInfo>> GetAllRecordingsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPVR] Start GetRecordings Async, retrieve all 'Pending', 'Inprogress' and 'Completed' recordings ");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/ManageService/Get/SortedFilteredList?sid={1}", baseUrl, Sid),
                DecompressionMethod = CompressionMethods.None
            };

            var filterOptions = new
            {
                resultLimit = -1,
                datetimeSortSeq = 0,
                channelSortSeq = 0,
                titleSortSeq = 0,
                statusSortSeq = 0,
                datetimeDecending = false,
                channelDecending = false,
                titleDecending = false,
                statusDecending = false,
                All = false,
                None = false,
                Pending = false,
                InProgress = true,
                Completed = true,
                Failed = true,
                Conflict = false,
                Recurring = false,
                Deleted = false,
                FilterByName = false,
                NameFilter = (string)null,
                NameFilterCaseSensative = false
            };

            options.RequestContent = _jsonSerializer.SerializeToString(filterOptions).ToString();
            options.RequestContentType = "application/json";

            var response = await _httpClient.Post(options).ConfigureAwait(false);

            using (var stream = response.Content)
            {
                return new RecordingResponse(baseUrl, _fileSystem).GetRecordings(stream, _jsonSerializer, _logger);
            }
        }

        /// <summary>
        /// Delete the Recording async from the disk
        /// </summary>
        /// <param name="recordingId">The recordingId</param>
        /// <param name="cancellationToken">The cancellationToken</param>
        /// <returns></returns>
        public async Task DeleteRecordingAsync(string recordingId, CancellationToken cancellationToken)
        {
            _logger.LogInformation(string.Format("[NextPVR] Start Delete Recording Async for recordingId: {0}", recordingId));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/ScheduleService/Delete/{1}?sid={2}", baseUrl, recordingId, Sid)
            };

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                LastRecordingChange = DateTimeOffset.UtcNow;

                bool? error = new CancelDeleteRecordingResponse().RecordingError(stream, _jsonSerializer, _logger);

                if (error == null || error == true)
                {
                    _logger.LogError(string.Format("[NextPVR] Failed to delete the recording for recordingId: {0}", recordingId));
                    throw new Exception(string.Format("Failed to delete the recording for recordingId: {0}", recordingId));
                }

                _logger.LogInformation("[NextPVR] Deleted Recording with recordingId: {0}", recordingId);
            }
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return "Next Pvr"; }
        }

        /// <summary>
        /// Cancel pending scheduled Recording
        /// </summary>
        /// <param name="timerId">The timerId</param>
        /// <param name="cancellationToken">The cancellationToken</param>
        /// <returns></returns>
        public async Task CancelTimerAsync(string timerId, CancellationToken cancellationToken)
        {
            _logger.LogInformation(string.Format("[NextPVR] Start Cancel Recording Async for recordingId: {0}", timerId));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/ScheduleService/CancelRec/{1}?sid={2}", baseUrl, timerId, Sid)
            };

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                LastRecordingChange = DateTimeOffset.UtcNow;
                bool? error = new CancelDeleteRecordingResponse().RecordingError(stream, _jsonSerializer, _logger);

                if (error == null || error == true)
                {
                    _logger.LogError(string.Format("[NextPVR] Failed to cancel the recording for recordingId: {0}", timerId));
                    throw new Exception(string.Format("Failed to cancel the recording for recordingId: {0}", timerId));
                }

                _logger.LogInformation(string.Format("[NextPVR] Cancelled Recording for recordingId: {0}", timerId));
            }
        }

        /// <summary>
        /// Create a new recording
        /// </summary>
        /// <param name="info">The TimerInfo</param>
        /// <param name="cancellationToken">The cancellationToken</param>
        /// <returns></returns>
        public async Task CreateTimerAsync(TimerInfo info, CancellationToken cancellationToken)
        {
            _logger.LogInformation(string.Format("[NextPVR] Start CreateTimer Async for ChannelId: {0} & Name: {1}", info.ChannelId, info.Name));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/ScheduleService/Record?sid={1}", baseUrl, Sid),
                DecompressionMethod = CompressionMethods.None
            };

            var timerSettings = await GetDefaultScheduleSettings(cancellationToken).ConfigureAwait(false);

            timerSettings.allChannels = false;
            timerSettings.ChannelOID = int.Parse(info.ChannelId, _usCulture);

            if (!string.IsNullOrEmpty(info.ProgramId))
            {
                timerSettings.epgeventOID = int.Parse(info.ProgramId, _usCulture);
            }

            timerSettings.post_padding_min = info.PostPaddingSeconds / 60;
            timerSettings.pre_padding_min = info.PrePaddingSeconds / 60;

            var postContent = _jsonSerializer.SerializeToString(timerSettings);
            UtilsHelper.DebugInformation(_logger, string.Format("[NextPVR] TimerSettings CreateTimer: {0} for ChannelId: {1} & Name: {2}", postContent, info.ChannelId, info.Name));

            options.RequestContent = postContent;
            options.RequestContentType = "application/json";

            try
            {
                await _httpClient.Post(options).ConfigureAwait((false));
            }
            catch (HttpException ex)
            {
                _logger.LogError(string.Format("[NextPVR] CreateTimer async with exception: {0}", ex.Message));
                throw new LiveTvConflictException();
            }
        }

        /// <summary>
        /// Get the pending Recordings.
        /// </summary>
        /// <param name="cancellationToken">The CancellationToken</param>
        /// <returns></returns>
        public async Task<IEnumerable<TimerInfo>> GetTimersAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPVR] Start GetTimer Async, retrieve the 'Pending' recordings");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/ManageService/Get/SortedFilteredList?sid={1}", baseUrl, Sid),
                DecompressionMethod = CompressionMethods.None
            };

            var filterOptions = new
            {
                resultLimit = -1,
                datetimeSortSeq = 0,
                channelSortSeq = 0,
                titleSortSeq = 0,
                statusSortSeq = 0,
                datetimeDecending = false,
                channelDecending = false,
                titleDecending = false,
                statusDecending = false,
                All = false,
                None = false,
                Pending = true,
                InProgress = false,
                Completed = false,
                Failed = false,
                Conflict = true,
                Recurring = false,
                Deleted = false,
                FilterByName = false,
                NameFilter = (string)null,
                NameFilterCaseSensative = false
            };

            options.RequestContent = _jsonSerializer.SerializeToString(filterOptions);
            options.RequestContentType = "application/json";

            var response = await _httpClient.Post(options).ConfigureAwait(false);

            using (var stream = response.Content)
            {
                return new RecordingResponse(baseUrl, _fileSystem).GetTimers(stream, _jsonSerializer, _logger);
            }
        }

        /// <summary>
        /// Get the recurrent recordings
        /// </summary>
        /// <param name="cancellationToken">The CancellationToken</param>
        /// <returns></returns>
        public async Task<IEnumerable<SeriesTimerInfo>> GetSeriesTimersAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPVR] Start GetSeriesTimer Async, retrieve the recurring recordings");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/ManageService/Get/SortedFilteredList?sid={1}", baseUrl, Sid),
                DecompressionMethod = CompressionMethods.None
            };

            var filterOptions = new
            {
                resultLimit = -1,
                All = false,
                None = false,
                Pending = false,
                InProgress = false,
                Completed = false,
                Failed = false,
                Conflict = false,
                Recurring = true,
                Deleted = false
            };

            options.RequestContent = _jsonSerializer.SerializeToString(filterOptions);
            options.RequestContentType = "application/json";

            var response = await _httpClient.Post(options).ConfigureAwait(false);

            using (var stream = response.Content)
            {
                return new RecordingResponse(baseUrl, _fileSystem).GetSeriesTimers(stream, _jsonSerializer, _logger);
            }
        }

        /// <summary>
        /// Create a recurrent recording
        /// </summary>
        /// <param name="info">The recurrend program info</param>
        /// <param name="cancellationToken">The CancelationToken</param>
        /// <returns></returns>
        public async Task CreateSeriesTimerAsync(SeriesTimerInfo info, CancellationToken cancellationToken)
        {
            _logger.LogInformation(string.Format("[NextPVR] Start CreateSeriesTimer Async for ChannelId: {0} & Name: {1}", info.ChannelId, info.Name));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/ScheduleService/Record?sid={1}", baseUrl, Sid),
                DecompressionMethod = CompressionMethods.None
            };

            var timerSettings = await GetDefaultScheduleSettings(cancellationToken).ConfigureAwait(false);

            timerSettings.allChannels = info.RecordAnyChannel;
            timerSettings.onlyNew = info.RecordNewOnly;
            timerSettings.recurringName = info.Name;
            timerSettings.recordAnyTimeslot = info.RecordAnyTime;

            if (!info.RecordAnyTime)
            {
                timerSettings.startDate = info.StartDate.ToString(_usCulture);
                timerSettings.endDate = info.EndDate.ToString(_usCulture);
                timerSettings.recordThisTimeslot = true;
            }

            if (info.Days.Count == 1)
            {
                timerSettings.recordThisDay = true;
            }

            if (info.Days.Count > 1 && info.Days.Count < 7)
            {
                timerSettings.recordSpecificdays = true;
            }

            timerSettings.recordAnyDay = info.Days.Count == 7;
            timerSettings.daySunday = info.Days.Contains(DayOfWeek.Sunday);
            timerSettings.dayMonday = info.Days.Contains(DayOfWeek.Monday);
            timerSettings.dayTuesday = info.Days.Contains(DayOfWeek.Tuesday);
            timerSettings.dayWednesday = info.Days.Contains(DayOfWeek.Wednesday);
            timerSettings.dayThursday = info.Days.Contains(DayOfWeek.Thursday);
            timerSettings.dayFriday = info.Days.Contains(DayOfWeek.Friday);
            timerSettings.daySaturday = info.Days.Contains(DayOfWeek.Saturday);

            if (!info.RecordAnyChannel)
            {
                timerSettings.ChannelOID = int.Parse(info.ChannelId, _usCulture);
            }

            if (!string.IsNullOrEmpty(info.ProgramId))
            {
                timerSettings.epgeventOID = int.Parse(info.ProgramId, _usCulture);
            }

            timerSettings.post_padding_min = info.PostPaddingSeconds / 60;
            timerSettings.pre_padding_min = info.PrePaddingSeconds / 60;

            var postContent = _jsonSerializer.SerializeToString(timerSettings);
            UtilsHelper.DebugInformation(_logger, string.Format("[NextPVR] TimerSettings CreateSeriesTimer: {0} for ChannelId: {1} & Name: {2}", postContent, info.ChannelId, info.Name));

            options.RequestContent = postContent;
            options.RequestContentType = "application/json";

            try
            {
                await _httpClient.Post(options).ConfigureAwait((false));
            }
            catch (HttpException ex)
            {
                _logger.LogError(string.Format("[NextPVR] CreateSeries async with exception: {0} ", ex.Message));
                throw new LiveTvConflictException();
            }
        }

        /// <summary>
        /// Update the series Timer
        /// </summary>
        /// <param name="info">The series program info</param>
        /// <param name="cancellationToken">The CancellationToken</param>
        /// <returns></returns>
        public async Task UpdateSeriesTimerAsync(SeriesTimerInfo info, CancellationToken cancellationToken)
        {
            _logger.LogInformation(string.Format("[NextPVR] Start UpdateSeriesTimer Async for ChannelId: {0} & Name: {1}", info.ChannelId, info.Name));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/ScheduleService/UpdateRecurr?sid={1}", baseUrl, Sid),
                DecompressionMethod = CompressionMethods.None
            };

            var timerSettings = await GetDefaultScheduleSettings(cancellationToken).ConfigureAwait(false);

            timerSettings.recurrOID = int.Parse(info.Id);
            timerSettings.post_padding_min = info.PostPaddingSeconds / 60;
            timerSettings.pre_padding_min = info.PrePaddingSeconds / 60;
            timerSettings.allChannels = info.RecordAnyChannel;
            timerSettings.onlyNew = info.RecordNewOnly;
            timerSettings.recurringName = info.Name;
            timerSettings.recordAnyTimeslot = info.RecordAnyTime;
            timerSettings.keep_all_days = true;
            timerSettings.days_to_keep = 0;
            timerSettings.extend_end_time_min = 0;

            var postContent = _jsonSerializer.SerializeToString(timerSettings);
            UtilsHelper.DebugInformation(_logger, string.Format("[NextPVR] TimerSettings UpdateSeriesTimer: {0} for ChannelId: {1} & Name: {2}", postContent, info.ChannelId, info.Name));

            options.RequestContent = postContent;
            options.RequestContentType = "application/json";

            try
            {
                await _httpClient.Post(options).ConfigureAwait((false));
            }
            catch (HttpException ex)
            {
                _logger.LogError(string.Format("[NextPVR] UpdateSeries async with exception: {0}", ex.Message));
                throw new LiveTvConflictException();
            }
        }

        /// <summary>
        /// Update a single Timer
        /// </summary>
        /// <param name="info">The program info</param>
        /// <param name="cancellationToken">The CancellationToken</param>
        /// <returns></returns>
        public async Task UpdateTimerAsync(TimerInfo info, CancellationToken cancellationToken)
        {
            _logger.LogInformation(string.Format("[NextPVR] Start UpdateTimer Async for ChannelId: {0} & Name: {1}", info.ChannelId, info.Name));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/ScheduleService/UpdateRec?sid={1}", baseUrl, Sid),
                DecompressionMethod = CompressionMethods.None
            };

            var timerSettings = await GetDefaultScheduleSettings(cancellationToken).ConfigureAwait(false);

            timerSettings.scheduleOID = int.Parse(info.Id);
            timerSettings.post_padding_min = info.PostPaddingSeconds / 60;
            timerSettings.pre_padding_min = info.PrePaddingSeconds / 60;

            var postContent = _jsonSerializer.SerializeToString(timerSettings);
            UtilsHelper.DebugInformation(_logger, string.Format("[NextPVR] TimerSettings UpdateTimer: {0} for ChannelId: {1} & Name: {2}", postContent, info.ChannelId, info.Name));

            options.RequestContent = postContent;
            options.RequestContentType = "application/json";

            try
            {
                await _httpClient.Post(options).ConfigureAwait((false));
                LastRecordingChange = DateTimeOffset.UtcNow;
            }
            catch (HttpException ex)
            {
                LastRecordingChange = DateTimeOffset.UtcNow;
                _logger.LogError(string.Format("[NextPVR] UpdateTimer Async with exception: {0}", ex.Message));
                throw new LiveTvConflictException();
            }
        }

        /// <summary>
        /// Cancel the Series Timer
        /// </summary>
        /// <param name="timerId">The Timer Id</param>
        /// <param name="cancellationToken">The CancellationToken</param>
        /// <returns></returns>
        public async Task CancelSeriesTimerAsync(string timerId, CancellationToken cancellationToken)
        {
            _logger.LogInformation(string.Format("[NextPVR] Start Cancel SeriesRecording Async for recordingId: {0}", timerId));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/ScheduleService/CancelRecurr/{1}?sid={2}", baseUrl, timerId, Sid)
            };

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                bool? error = new CancelDeleteRecordingResponse().RecordingError(stream, _jsonSerializer, _logger);

                if (error == null || error == true)
                {
                    _logger.LogError(string.Format("[NextPVR] Failed to cancel the recording with recordingId: {0}", timerId));
                    throw new Exception(string.Format("Failed to cancel the recording with recordingId: {0}", timerId));
                }

                _logger.LogInformation("[NextPVR] Cancelled Recording for recordingId: {0}", timerId);
            }
        }

        /// <summary>
        /// Get the DefaultScheduleSettings
        /// </summary>
        /// <param name="cancellationToken">The CancellationToken</param>
        /// <returns></returns>
        private async Task<ScheduleSettings> GetDefaultScheduleSettings(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPVR] Start GetDefaultScheduleSettings");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/ScheduleService/Get/SchedSettingsObj?sid={1}", baseUrl, Sid)
            };

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                return new TimerDefaultsResponse().GetScheduleSettings(stream, _jsonSerializer);
            }
        }

        public Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(string channelId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<List<MediaSourceInfo>> GetRecordingStreamMediaSources(string recordingId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<MediaSourceInfo> GetChannelStream(string channelOid, string mediaSourceId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPVR] Start ChannelStream");
            var config = Plugin.Instance.Configuration;
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;
            _liveStreams++;

            string streamUrl = string.Format("{0}/live?channeloid={1}&client=Jellyfin.{2}", baseUrl, channelOid, _liveStreams.ToString());
            _logger.LogInformation("[NextPVR] Streaming " + streamUrl);
            return new MediaSourceInfo
            {
                Id = _liveStreams.ToString(CultureInfo.InvariantCulture),
                Path = streamUrl,
                Protocol = MediaProtocol.Http,
                MediaStreams = new List<MediaStream>
                        {
                            new MediaStream
                            {
                                Type = MediaStreamType.Video,
                                IsInterlaced = true,
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
        }

        public async Task<MediaSourceInfo> GetRecordingStream(string recordingId, string mediaSourceId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPVR] Start GetRecordingStream");
            var recordings = await GetRecordingsAsync(cancellationToken).ConfigureAwait(false);
            var recording = recordings.First(i => string.Equals(i.Id, recordingId, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(recording.Url))
            {
                _logger.LogInformation("[NextPVR] RecordingUrl: {0}", recording.Url);
                return new MediaSourceInfo
                {
                    Path = recording.Url,
                    Protocol = MediaProtocol.Http,
                    MediaStreams = new List<MediaStream>
                        {
                            new MediaStream
                            {
                                Type = MediaStreamType.Video,

                                // Set the index to -1 because we don't know the exact index of the video stream within the container
                                Index = -1,

                                // Set to true if unknown to enable deinterlacing
                                IsInterlaced = true
                            },
                            new MediaStream
                            {
                                Type = MediaStreamType.Audio,
                                // Set the index to -1 because we don't know the exact index of the audio stream within the container
                                Index = -1
                            }
                        },

                    Container = "mpegts"
                };
            }

            if (!string.IsNullOrEmpty(recording.Path) && File.Exists(recording.Path))
            {
                _logger.LogInformation("[NextPVR] RecordingPath: {0}", recording.Path);
                return new MediaSourceInfo
                {
                    Path = recording.Path,
                    Protocol = MediaProtocol.File,
                    MediaStreams = new List<MediaStream>
                        {
                            new MediaStream
                            {
                                Type = MediaStreamType.Video,
                                // Set the index to -1 because we don't know the exact index of the video stream within the container
                                Index = -1,

                                // Set to true if unknown to enable deinterlacing
                                IsInterlaced = true
                            },
                            new MediaStream
                            {
                                Type = MediaStreamType.Audio,
                                // Set the index to -1 because we don't know the exact index of the audio stream within the container
                                Index = -1
                            }
                        },

                    Container = "mpegts"
                };
            }

            _logger.LogError("[NextPVR] No stream exists for recording {0}", recording);
            throw new ResourceNotFoundException(string.Format("No stream exists for recording {0}", recording));
        }

        public async Task CloseLiveStream(string id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPVR] Closing " + id);
        }

        public async Task<SeriesTimerInfo> GetNewTimerDefaultsAsync(CancellationToken cancellationToken, ProgramInfo program = null)
        {
            _logger.LogInformation("[NextPVR] Start GetNewTimerDefault Async");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions()
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/ScheduleService/Get/SchedSettingsObj?sid{1}", baseUrl, Sid)
            };

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                return new TimerDefaultsResponse().GetDefaultTimerInfo(stream, _jsonSerializer, _logger);
            }
        }

        public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(string channelId, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPVR] Start GetPrograms Async, retrieve all Programs");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions()
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/GuideService/Listing?sid={1}&stime={2}&etime={3}&channelId={4}",
                baseUrl, Sid,
                DateTimeHelper.getUnixUTCTimeFromUtcDateTime(startDateUtc).ToString(_usCulture),
                DateTimeHelper.getUnixUTCTimeFromUtcDateTime(endDateUtc).ToString(_usCulture),
                channelId)
            };

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                return new ListingsResponse(baseUrl).GetPrograms(stream, _jsonSerializer, channelId, _logger).ToList();
            }
        }

        public Task RecordLiveStream(string id, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public event EventHandler DataSourceChanged;

        public event EventHandler<RecordingStatusChangedEventArgs> RecordingStatusChanged;

        public async Task<LiveTvServiceStatusInfo> GetStatusInfoAsync(CancellationToken cancellationToken)
        {
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            //Version Check
            var options = new HttpRequestOptions()
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/Util/NPVR/VersionCheck?sid={1}", baseUrl, Sid)
            };

            bool upgradeAvailable;
            string serverVersion;

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                var versionCheckResponse = new VersionCheckResponse(stream, _jsonSerializer);

                upgradeAvailable = versionCheckResponse.UpdateAvailable();
                serverVersion = versionCheckResponse.ServerVersion();
            }


            //Tuner information
            var optionsTuner = new HttpRequestOptions()
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/public/Util/Tuner/Stat?sid={1}", baseUrl, Sid)
            };

            List<LiveTvTunerInfo> tvTunerInfos;
            using (var stream = await _httpClient.Get(optionsTuner).ConfigureAwait(false))
            {
                var tuners = new TunerResponse(stream, _jsonSerializer);
                tvTunerInfos = tuners.LiveTvTunerInfos();
            }

            return new LiveTvServiceStatusInfo
            {
                HasUpdateAvailable = upgradeAvailable,
                Version = serverVersion,
                Tuners = tvTunerInfos
            };
        }

        public string HomePageUrl
        {
            get { return "http://www.nextpvr.com/"; }
        }

        public Task ResetTuner(string id, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ImageStream> GetChannelImageAsync(string channelId, CancellationToken cancellationToken)
        {
            // Leave as is. This is handled by supplying image url to ChannelInfo
            throw new NotImplementedException();
        }

        public Task<ImageStream> GetProgramImageAsync(string programId, string channelId, CancellationToken cancellationToken)
        {
            // Leave as is. This is handled by supplying image url to ProgramInfo
            throw new NotImplementedException();
        }

        public Task<ImageStream> GetRecordingImageAsync(string recordingId, CancellationToken cancellationToken)
        {
            // Leave as is. This is handled by supplying image url to RecordingInfo
            throw new NotImplementedException();
        }
    }
}
