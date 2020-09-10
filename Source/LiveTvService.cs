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
        public bool isActive { get { return Sid != null; } }
        private DateTimeOffset LastUpdatedSidDateTime { get; set; }
        private IFileSystem _fileSystem;

        public DateTimeOffset LastRecordingChange = DateTimeOffset.MinValue;

        //public static LiveTvService Instance { get; private set; }


        public LiveTvService(IHttpClient httpClient, IJsonSerializer jsonSerializer, ILoggerFactory loggerFactory, ICryptoProvider cryptoProvider, IFileSystem fileSystem)
        {
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
            _logger = loggerFactory.CreateLogger<LiveTvService>();
            Sid = null;
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
                _logger.LogError("[NextPvr] Web service url must be configured.");
                throw new InvalidOperationException("NextPvr web service url must be configured.");
            }

            if (string.IsNullOrEmpty(config.Pin))
            {
                _logger.LogError("[NextPvr] Pin must be configured.");
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
            _logger.LogInformation("[NextPvr] Start InitiateSession");
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=session.initiate&ver=1.0&device=emby", baseUrl)
            };
            options.AcceptHeader = "application/json";
            options.LogErrorResponseBody = false;
            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                var clientKeys = new InstantiateResponse().GetClientKeys(stream, _jsonSerializer, _logger);

                var sid = clientKeys.sid;
                var salt = clientKeys.salt;
                _logger.LogInformation(string.Format("[NextPvr] Sid: {0}", sid));

                var loggedIn = await Login(sid, salt, cancellationToken).ConfigureAwait(false);

                if (loggedIn)
                {
                    _logger.LogInformation("[NextPVR] Session initiated.");
                    Sid = sid;
                    LastUpdatedSidDateTime = DateTimeOffset.UtcNow;
                    bool flag = await GetDefaultSettingsAsync(cancellationToken);
                    Plugin.Instance.Configuration.GetEpisodeImage = "true" == await GetBackendSettingAsync(cancellationToken, "/Settings/General/ArtworkFromSchedulesDirect");
                }
                else
                {
                    _logger.LogError("[NextPVR] PIN not accepted.");
                    throw new UnauthorizedAccessException("NextPVR PIN not accepted");
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
            _logger.LogInformation(string.Format("[NextPvr] Start Login procedure for Sid: {0} & Salt: {1}", sid, salt));
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;
            var pin = Plugin.Instance.Configuration.Pin;
            _logger.LogInformation(string.Format("[NextPvr] Pin: {0}", pin));

            var strb = new StringBuilder();
            var md5Result = GetMd5Hash(strb.Append(":").Append(GetMd5Hash(pin)).Append(":").Append(salt).ToString());

            var options = new HttpRequestOptions
            {
                Url = string.Format("{0}/service?method=session.login&md5={1}&sid={2}", baseUrl, md5Result, sid),
                CancellationToken = cancellationToken
            };
            options.AcceptHeader = "application/json";
            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                return new InitializeResponse().LoggedIn(stream, _jsonSerializer, _logger);
            }
        }

        public string GetMd5Hash(string value)
        {
            byte[] hashValue;
            hashValue = System.Security.Cryptography.MD5.Create().ComputeHash(new UTF8Encoding().GetBytes(value));
            //Bit convertor return the byte to string as all caps hex values seperated by "-"
            return BitConverter.ToString(hashValue).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Gets the channels async.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{IEnumerable{ChannelInfo}}.</returns>
        public async Task<IEnumerable<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPvr] Start GetChannels Async, retrieve all channels");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=channel.list&sid={1}", baseUrl, Sid)
            };
            options.AcceptHeader = "application/json";

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
            _logger.LogInformation("[NextPvr] Start GetRecordings Async, retrieve all 'Pending', 'Inprogress' and 'Completed' recordings ");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=recording.list&filter=ready&sid={1}", baseUrl, Sid)
            };

            options.AcceptHeader = "application/json";

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
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
            _logger.LogInformation(string.Format("[NextPvr] Start Delete Recording Async for recordingId: {0}", recordingId));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=recording.delete&recording_id={1}&sid={2}", baseUrl, recordingId, Sid)
            };
            options.AcceptHeader = "application/json";

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                LastRecordingChange = DateTimeOffset.UtcNow;

                bool? error = new CancelDeleteRecordingResponse().RecordingError(stream, _jsonSerializer, _logger);

                if (error == null || error == true)
                {
                    _logger.LogError(string.Format("[NextPvr] Failed to delete the recording for recordingId: {0}", recordingId));
                    throw new Exception(string.Format("Failed to delete the recording for recordingId: {0}", recordingId));
                }

                _logger.LogInformation("[NextPvr] Deleted Recording with recordingId: {0}", recordingId);
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
            _logger.LogInformation(string.Format("[NextPvr] Start Cancel Recording Async for recordingId: {0}", timerId));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=recording.delete&recording_id={1}&sid={2}", baseUrl, timerId, Sid)
            };
            options.AcceptHeader = "application/json";
            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                LastRecordingChange = DateTimeOffset.UtcNow;
                bool? error = new CancelDeleteRecordingResponse().RecordingError(stream, _jsonSerializer, _logger);

                if (error == null || error == true)
                {
                    _logger.LogError(string.Format("[NextPvr] Failed to cancel the recording for recordingId: {0}", timerId));
                    throw new Exception(string.Format("Failed to cancel the recording for recordingId: {0}", timerId));
                }

                _logger.LogInformation(string.Format("[NextPvr] Cancelled Recording for recordingId: {0}", timerId));
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
            _logger.LogInformation(string.Format("[NextPvr] Start CreateTimer Async for ChannelId: {0} & Name: {1}", info.ChannelId, info.Name));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=recording.save&sid={1}&event_id={2}&pre_padding={3}&post_padding={4}", baseUrl, Sid,
                 int.Parse(info.ProgramId, _usCulture),
                 info.PrePaddingSeconds / 60,
                 info.PostPaddingSeconds / 60,
                 info.Id
                )
            };
            UtilsHelper.DebugInformation(_logger, string.Format("[NextPVR] TimerSettings CreateTimer: {0} for ChannelId: {1} & Name: {2}", info.ProgramId, info.ChannelId, info.Name));

            options.AcceptHeader = "application/json";

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                bool? error = new CancelDeleteRecordingResponse().RecordingError(stream, _jsonSerializer, _logger);
                if (error == null || error == true)
                {
                    _logger.LogError(string.Format("[NextPVR] Failed to create the timer with programId: {0}", info.ProgramId));
                    throw new Exception(string.Format("Failed to create the timer with programId: {0}", info.ProgramId));
                }
                _logger.LogError("[NextPVR] CreateTimer async for programId: {0}", info.ProgramId);
            }
        }

        /// <summary>
        /// Get the pending Recordings.
        /// </summary>
        /// <param name="cancellationToken">The CancellationToken</param>
        /// <returns></returns>
        public async Task<IEnumerable<TimerInfo>> GetTimersAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPvr] Start GetTimer Async, retrieve the 'Pending' recordings");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=recording.list&filter=pending&sid={1}", baseUrl, Sid)
            };

            options.AcceptHeader = "application/json";
            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
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
            _logger.LogInformation("[NextPvr] Start GetSeriesTimer Async, retrieve the recurring recordings");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=recording.recurring.list&sid={1}", baseUrl, Sid)
            };

            options.AcceptHeader = "application/json";
            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                return new RecurringResponse(baseUrl, _fileSystem).GetSeriesTimers(stream, _jsonSerializer, _logger);
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
            _logger.LogInformation(string.Format("[NextPvr] Start CreateSeriesTimer Async for ChannelId: {0} & Name: {1}", info.ChannelId, info.Name));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;


            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=recording.recurring.save&sid={1}&pre_padding={2}&post_padding={3}&keep={4}", baseUrl, Sid,
                    info.PrePaddingSeconds / 60,
                    info.PostPaddingSeconds / 60,
                info.KeepUpTo)
            };

            options.AcceptHeader = "application/json";

            int recurringType = int.Parse(Plugin.Instance.Configuration.RecordingDefault);

            if (recurringType == 99)
            {
                options.Url += string.Format("&name={0}&keyword=title+like+'{0}'", Uri.EscapeUriString(info.Name.Replace("'", "''")));
            }
            else
            {
                options.Url += string.Format("&event_id={0}&recurring_type={1}", info.ProgramId, recurringType);
            }
            if (info.RecordNewOnly || Plugin.Instance.Configuration.NewEpisodes)
                options.Url += "&only_new=true";

            if (recurringType == 3 || recurringType == 4)
                options.Url += "&timeslot=true";

            await CreateUpdateSeriesTimerAsync(info, options);
        }

        /// <summary>
        /// Update the series Timer
        /// </summary>
        /// <param name="info">The series program info</param>
        /// <param name="cancellationToken">The CancellationToken</param>
        /// <returns></returns>
        public async Task CreateUpdateSeriesTimerAsync(SeriesTimerInfo info, HttpRequestOptions options)
        {
            UtilsHelper.DebugInformation(_logger, string.Format("[NextPVR] TimerSettings CreateSeriesTimerAsync: {0} for ChannelId: {1} & Name: {2}", info.ProgramId, info.ChannelId, info.Name));
            options.AcceptHeader = "application/json";

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                bool? error = new CancelDeleteRecordingResponse().RecordingError(stream, _jsonSerializer, _logger);
                if (error == null || error == true)
                {
                    _logger.LogError(string.Format("[NextPVR] Failed to create or update the timer with Recurring ID: {0}", info.Id));
                    throw new Exception(string.Format("Failed to create or update the timer with Recurring ID: {0}", info.Id));
                }
                _logger.LogInformation("[NextPVR] CreateUpdateSeriesTimer async for Program ID: {0} Recurring ID {1}", info.ProgramId, info.Id);
                //Thread.Sleep(1000);
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
                Url = string.Format("{0}/service?method=recording.recurring.save&sid={1}&pre_padding={2}&post_padding={3}&keep={4}&recurring_id={5}", baseUrl, Sid,
                 info.PrePaddingSeconds / 60,
                 info.PostPaddingSeconds / 60,
                info.KeepUpTo,
                info.Id)
            };

            int recurringType = 2;

            if (info.RecordAnyChannel)
            {
                options.Url += string.Format("&name={0}&keyword=title+like+'{0}'", Uri.EscapeUriString(info.Name.Replace("'", "''")));
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
                    if (info.Days.Count == 7)
                    {
                        recurringType = 4;
                    }
                    else
                    {
                        recurringType = 3;
                    }
                }
                options.Url += string.Format("&recurring_type={0}", recurringType);
            }
            if (info.RecordNewOnly)
                options.Url += "&only_new=true";

            await CreateUpdateSeriesTimerAsync(info, options);

        }


        /// <summary>
        /// Update a single Timer
        /// </summary>
        /// <param name="info">The program info</param>
        /// <param name="cancellationToken">The CancellationToken</param>
        /// <returns></returns>
        public async Task UpdateTimerAsync(TimerInfo info, CancellationToken cancellationToken)
        {
            _logger.LogInformation(string.Format("[NextPvr] Start UpdateTimer Async for ChannelId: {0} & Name: {1}", info.ChannelId, info.Name));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=recording.save&sid={1}&pre_padding={2}&post_padding={3}&recording_id={4}&event_id={5}", baseUrl, Sid,
                 info.PrePaddingSeconds / 60,
                 info.PostPaddingSeconds / 60,
                 info.Id,
                info.ProgramId)
            };
            options.AcceptHeader = "application/json";
            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                bool? error = new CancelDeleteRecordingResponse().RecordingError(stream, _jsonSerializer, _logger);
                if (error == null || error == true)
                {
                    _logger.LogError(string.Format("[NextPVR] Failed to update the timer with ID: {0}", info.Id));
                    throw new Exception(string.Format("Failed to update the timer with ID: {0}", info.Id));
                }
                _logger.LogInformation("[NextPVR] UpdateTimer async for Program ID: {0} ID {1}", info.ProgramId, info.Id);
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
            _logger.LogInformation(string.Format("[NextPvr] Start Cancel SeriesRecording Async for recordingId: {0}", timerId));
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=recording.recurring.delete&recurring_id={1}&sid={2}", baseUrl, timerId, Sid)
            };
            options.AcceptHeader = "application/json";
            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                bool? error = new CancelDeleteRecordingResponse().RecordingError(stream, _jsonSerializer, _logger);

                if (error == null || error == true)
                {
                    _logger.LogError(string.Format("[NextPvr] Failed to cancel the recording with recordingId: {0}", timerId));
                    throw new Exception(string.Format("Failed to cancel the recording with recordingId: {0}", timerId));
                }

                _logger.LogInformation("[NextPvr] Cancelled Recording for recordingId: {0}", timerId);
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
            _logger.LogInformation("[NextPvr] Start ChannelStream");
            var config = Plugin.Instance.Configuration;
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;
            _liveStreams++;

            string streamUrl = string.Format("{0}/live?channeloid={1}&client=emby.{2}", baseUrl, channelOid, _liveStreams.ToString());
            _logger.LogInformation("[NextPvr] Streaming " + streamUrl);
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
            _logger.LogInformation("[NextPvr] Start GetRecordingStream");
            var recordings = await GetRecordingsAsync(cancellationToken).ConfigureAwait(false);
            var recording = recordings.First(i => string.Equals(i.Id, recordingId, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(recording.Url))
            {
                _logger.LogInformation("[NextPvr] RecordingUrl: {0}", recording.Url);
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
                _logger.LogInformation("[NextPvr] RecordingPath: {0}", recording.Path);
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

            _logger.LogError("[NextPvr] No stream exists for recording {0}", recording);
            throw new ResourceNotFoundException(string.Format("No stream exists for recording {0}", recording));
        }

        public async Task CloseLiveStream(string id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPvr] Closing " + id);
        }

        public async Task<SeriesTimerInfo> GetNewTimerDefaultsAsync(CancellationToken cancellationToken, ProgramInfo program = null)
        {
            SeriesTimerInfo defaultSettings = new SeriesTimerInfo();
            defaultSettings.PrePaddingSeconds = Plugin.Instance.Configuration.PrePaddingSeconds;
            defaultSettings.PostPaddingSeconds = Plugin.Instance.Configuration.PostPaddingSeconds;
            return defaultSettings;
        }

        private async Task<bool> GetDefaultSettingsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPVR] Start GetDefaultSettings Async");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions()
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=setting.list&sid={1}", baseUrl, Sid)
            };
            options.AcceptHeader = "application/json";
            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                return new SettingResponse().GetDefaultSettings(stream, _jsonSerializer, _logger);
            }
        }
        public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(string channelId, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[NextPvr] Start GetPrograms Async, retrieve all Programs");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions()
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=channel.listings&sid={1}&start={2}&end={3}&channel_id={4}",
                baseUrl, Sid,
                (int)(startDateUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds,
                (int)(endDateUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds,
                channelId)
            };
            options.AcceptHeader = "application/json";
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
                Url = string.Format("{0}/service?method=setting.version&sid={1}", baseUrl, Sid)
            };

            options.AcceptHeader = "application/json";
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
                Url = string.Format("{0}/service/method=system.status?sid={1}", baseUrl, Sid)
            };
            options.AcceptHeader = "application/json";

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

        public async Task<DateTimeOffset> GetLastUpdate(CancellationToken cancellationToken)
        {
            _logger.LogDebug("[NextPVR] GetLastUpdateTime");
            DateTimeOffset retTime = DateTimeOffset.FromUnixTimeSeconds(0);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions
            {
                // don't use sid to avoid fake login
                Url = string.Format("{0}/service?method=recording.lastupdated&ignore_resume=true&sid={1}", baseUrl, Sid)
            };
            options.AcceptHeader = "application/json";
            options.LogErrorResponseBody = false;
            try
            {
                using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
                {
                    retTime = new LastUpdateResponse().GetUpdateTime(stream, _jsonSerializer, _logger);
                    if (retTime == DateTimeOffset.FromUnixTimeSeconds(0))
                    {
                        LastUpdatedSidDateTime = DateTimeOffset.MinValue;
                        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else if (LastUpdatedSidDateTime != DateTimeOffset.MinValue)
                    {
                        LastUpdatedSidDateTime = DateTime.UtcNow;
                    }

                }
            }
            catch (MediaBrowser.Model.Net.HttpException httpError)
            {
                if (httpError.IsTimedOut)
                {
                    System.Diagnostics.Debug.WriteLine("timed out");
                    LastUpdatedSidDateTime = DateTimeOffset.MinValue;
                }
            }
            catch (System.Net.Http.HttpRequestException)
            {
                System.Diagnostics.Debug.WriteLine("server not running");
                LastUpdatedSidDateTime = DateTimeOffset.MinValue;
            }
            catch (Exception err)
            {
                System.Diagnostics.Debug.WriteLine(err.StackTrace);
                System.Diagnostics.Debug.WriteLine(err.Message);
                throw;
            }
            _logger.LogInformation("[NextPVR] GetLastUpdateTime " + retTime.ToUnixTimeSeconds());
            return retTime;
        }

        public async Task<string> GetBackendSettingAsync(CancellationToken cancellationToken, string key)
        {
            _logger.LogInformation("[NextPVR] GetBackendSetting");
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var baseUrl = Plugin.Instance.Configuration.WebServiceUrl;

            var options = new HttpRequestOptions()
            {
                CancellationToken = cancellationToken,
                Url = string.Format("{0}/service?method=setting.get&key={1}&sid={2}", baseUrl, key, Sid)
            };
            options.AcceptHeader = "application/json";
            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                return new SettingResponse().GetSetting(stream, _jsonSerializer, _logger);
            }
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
