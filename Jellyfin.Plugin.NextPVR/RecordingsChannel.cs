using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions;
using Jellyfin.Plugin.NextPVR.Entities;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR;

public class RecordingsChannel : IChannel, IHasCacheKey, ISupportsDelete, ISupportsLatestMedia, ISupportsMediaProbe, IHasFolderAttributes, IDisposable, IHasItemChangeMonitor
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<RecordingsChannel> _logger;
    private readonly CancellationTokenSource _cancellationToken;
    private readonly string _recordingCacheDirectory;
    private static SemaphoreSlim _semaphore;

    private Timer _updateTimer;
    private DateTimeOffset _lastUpdate = DateTimeOffset.FromUnixTimeSeconds(0);

    private IEnumerable<MyRecordingInfo> _allRecordings;
    private bool _useCachedRecordings = false;
    private DateTime _cachedRecordingModificationTime;
    private string _cachekeyBase;
    private int _pollInterval = -1;

    public RecordingsChannel(IApplicationPaths applicationPaths, ILibraryManager libraryManager, IFileSystem fileSystem, ILogger<RecordingsChannel> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        string channelId = libraryManager.GetNewItemId($"Channel {Name}", typeof(Channel)).ToString("N", CultureInfo.InvariantCulture);
        string version = BaseExtensions.GetMD5($"{DataVersion}2").ToString("N", CultureInfo.InvariantCulture);
        _recordingCacheDirectory = Path.Join(applicationPaths.CachePath, "channels", channelId, version);
        CleanCache(true);
        _cancellationToken = new CancellationTokenSource();
        _semaphore = new SemaphoreSlim(1, 1);
    }

    public string Name => "NextPVR Recordings";

    public string Description => "NextPVR Recordings";

#pragma warning disable CA1819
    public string[] Attributes => new[] { "Recordings" };
#pragma warning restore CA1819

    public string DataVersion => "1";

    public string HomePageUrl => "https://www.nextpvr.com";

    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _updateTimer?.Dispose();
            _cancellationToken?.Cancel();
            _cancellationToken?.Dispose();
            _updateTimer = null;
        }
    }

    public string GetCacheKey(string userId)
    {
        DateTimeOffset dto = LiveTvService.Instance.RecordingModificationTime;
        return $"{dto.ToUnixTimeSeconds()}-{_cachekeyBase}";
    }

    private void CleanCache(bool cleanAll = false)
    {
        if (!string.IsNullOrEmpty(_recordingCacheDirectory) && Directory.Exists(_recordingCacheDirectory))
        {
            string[] cachedJson = Directory.GetFiles(_recordingCacheDirectory, "*.json");
            _logger.LogInformation("Cleaning JSON cache {0} {1}", _recordingCacheDirectory, cachedJson.Length);
            foreach (string fileName in cachedJson)
            {
                if (cleanAll == true || _fileSystem.GetLastWriteTimeUtc(fileName).Add(TimeSpan.FromHours(3)) <= DateTimeOffset.UtcNow)
                {
                    _fileSystem.DeleteFile(fileName);
                }
            }
        }

        return;
    }

    public InternalChannelFeatures GetChannelFeatures()
    {
        return new InternalChannelFeatures { ContentTypes = new List<ChannelMediaContentType> { ChannelMediaContentType.Movie, ChannelMediaContentType.Episode, ChannelMediaContentType.Clip }, MediaTypes = new List<ChannelMediaType> { ChannelMediaType.Audio, ChannelMediaType.Video }, SupportsContentDownloading = true };
    }

    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        if (type == ImageType.Primary)
        {
            return Task.FromResult(new DynamicImageResponse { Path = "https://repo.jellyfin.org/releases/plugin/images/jellyfin-plugin-nextpvr.png", Protocol = MediaProtocol.Http, HasImage = true });
        }

        return Task.FromResult(new DynamicImageResponse { HasImage = false });
    }

    public IEnumerable<ImageType> GetSupportedChannelImages()
    {
        return new List<ImageType> { ImageType.Primary };
    }

    public bool IsEnabledFor(string userId)
    {
        return true;
    }

    private LiveTvService GetService()
    {
        LiveTvService service = LiveTvService.Instance;
        if (service is not null && (!service.IsActive || _cachedRecordingModificationTime != Plugin.Instance.Configuration.RecordingModificationTime || service.FlagRecordingChange))
        {
            try
            {
                CancellationToken cancellationToken = CancellationToken.None;
                service.EnsureConnectionAsync(cancellationToken).Wait();
                if (service.IsActive)
                {
                    _useCachedRecordings = false;
                    if (_cachedRecordingModificationTime != Plugin.Instance.Configuration.RecordingModificationTime)
                    {
                        _cachedRecordingModificationTime = Plugin.Instance.Configuration.RecordingModificationTime;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        return service;
    }

    public bool CanDelete(BaseItem item)
    {
        if (_cachedRecordingModificationTime != Plugin.Instance.Configuration.RecordingModificationTime)
        {
            return false;
        }

        return !item.IsFolder;
    }

    public Task DeleteItem(string id, CancellationToken cancellationToken)
    {
        if (_cachedRecordingModificationTime != Plugin.Instance.Configuration.RecordingModificationTime)
        {
            return Task.FromException(new InvalidOperationException("Recordings not reloaded"));
        }

        var service = GetService();
        return service is null
            ? Task.CompletedTask
            : service.DeleteRecordingAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<ChannelItemInfo>> GetLatestMedia(ChannelLatestMediaSearch request, CancellationToken cancellationToken)
    {
        var result = await GetChannelItems(new InternalChannelItemQuery(), _ => true, cancellationToken).ConfigureAwait(false);

        return result.Items.OrderByDescending(i => i.DateCreated ?? DateTime.MinValue);
    }

    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        await GetRecordingsAsync("GetChannelItems", cancellationToken);

        if (string.IsNullOrWhiteSpace(query.FolderId))
        {
            return await GetRecordingGroups(query, cancellationToken);
        }

        if (query.FolderId.StartsWith("series_", StringComparison.OrdinalIgnoreCase))
        {
            var hash = query.FolderId.Split('_')[1];
            return await GetChannelItems(query, i => i.IsSeries && string.Equals(i.Name.GetMD5().ToString("N"), hash, StringComparison.Ordinal), cancellationToken);
        }

        if (string.Equals(query.FolderId, "kids", StringComparison.OrdinalIgnoreCase))
        {
            return await GetChannelItems(query, i => i.IsKids, cancellationToken);
        }

        if (string.Equals(query.FolderId, "movies", StringComparison.OrdinalIgnoreCase))
        {
            return await GetChannelItems(query, i => i.IsMovie, cancellationToken);
        }

        if (string.Equals(query.FolderId, "news", StringComparison.OrdinalIgnoreCase))
        {
            return await GetChannelItems(query, i => i.IsNews, cancellationToken);
        }

        if (string.Equals(query.FolderId, "sports", StringComparison.OrdinalIgnoreCase))
        {
            return await GetChannelItems(query, i => i.IsSports, cancellationToken);
        }

        if (string.Equals(query.FolderId, "others", StringComparison.OrdinalIgnoreCase))
        {
            return await GetChannelItems(query, i => !i.IsSports && !i.IsNews && !i.IsMovie && !i.IsKids && !i.IsSeries, cancellationToken);
        }

        var result = new ChannelItemResult() { Items = new List<ChannelItemInfo>() };

        return result;
    }

    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, Func<MyRecordingInfo, bool> filter, CancellationToken cancellationToken)
    {
        await GetRecordingsAsync("GetChannelItems", cancellationToken);
        List<ChannelItemInfo> pluginItems = new List<ChannelItemInfo>();
        pluginItems.AddRange(_allRecordings.Where(filter).Select(ConvertToChannelItem));
        var result = new ChannelItemResult() { Items = pluginItems };

        return result;
    }

    private ChannelItemInfo ConvertToChannelItem(MyRecordingInfo item)
    {
        var path = string.IsNullOrEmpty(item.Path) ? item.Url : item.Path;

        var channelItem = new ChannelItemInfo
        {
            Name = string.IsNullOrEmpty(item.EpisodeTitle) ? item.Name : item.EpisodeTitle,
            SeriesName = !string.IsNullOrEmpty(item.EpisodeTitle) || item.IsSeries ? item.Name : null,
            StartDate = item.StartDate,
            EndDate = item.EndDate,
            OfficialRating = item.OfficialRating,
            CommunityRating = item.CommunityRating,
            ContentType = item.IsMovie ? ChannelMediaContentType.Movie : ChannelMediaContentType.Episode,
            Genres = item.Genres,
            ImageUrl = item.ImageUrl,
            Id = item.Id,
            ParentIndexNumber = item.SeasonNumber,
            IndexNumber = item.EpisodeNumber,
            MediaType = item.ChannelType == ChannelType.TV ? ChannelMediaType.Video : ChannelMediaType.Audio,
            MediaSources = new List<MediaSourceInfo>
            {
                new MediaSourceInfo
                {
                    Path = path,
                    Container = item.Status == RecordingStatus.InProgress ? "ts" : null,
                    Protocol = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? MediaProtocol.Http : MediaProtocol.File,
                    BufferMs = 1000,
                    AnalyzeDurationMs = 0,
                    IsInfiniteStream = item.Status == RecordingStatus.InProgress,
                    TranscodingContainer = "ts",
                    RunTimeTicks = item.Status == RecordingStatus.InProgress ? null : (item.EndDate - item.StartDate).Ticks,
                }
            },
            PremiereDate = item.OriginalAirDate,
            ProductionYear = item.ProductionYear,
            Type = ChannelItemType.Media,
            DateModified = item.Status == RecordingStatus.InProgress ? DateTime.Now : Plugin.Instance.Configuration.RecordingModificationTime,
            Overview = item.Overview,
            IsLiveStream = item.Status != RecordingStatus.InProgress ? false : Plugin.Instance.Configuration.EnableInProgress,
            Etag = item.Status.ToString()
        };

        return channelItem;
    }

    private async Task<bool> GetRecordingsAsync(string name, CancellationToken cancellationToken)
    {
        var service = GetService();
        if (service is null || !service.IsActive)
        {
            return false;
        }

        if (_useCachedRecordings == false || service.FlagRecordingChange)
        {
            if (_pollInterval == -1)
            {
                var interval = TimeSpan.FromSeconds(Plugin.Instance.Configuration.PollInterval);
                _updateTimer = new Timer(OnUpdateTimerCallbackAsync, null, TimeSpan.FromMinutes(2), interval);
                if (_updateTimer != null)
                {
                    _pollInterval = Plugin.Instance.Configuration.PollInterval;
                }
            }

            if (await _semaphore.WaitAsync(30000, cancellationToken))
            {
                try
                {
                    _logger.LogDebug("{0} Reload cache", name);
                    _allRecordings = await service.GetAllRecordingsAsync(cancellationToken).ConfigureAwait(false);
                    int maxId = _allRecordings.Max(r => int.Parse(r.Id, CultureInfo.InvariantCulture));
                    int inProcessCount = _allRecordings.Where(r => r.Status == RecordingStatus.InProgress).Count();
                    string keyBase = $"{maxId}-{inProcessCount}-{_allRecordings.Count()}";
                    if (keyBase != _cachekeyBase && !service.FlagRecordingChange)
                    {
                        _logger.LogDebug("External recording list change {0}", keyBase);
                        CleanCache(true);
                    }

                    _cachekeyBase = keyBase;
                    _lastUpdate = DateTimeOffset.UtcNow;
                    service.FlagRecordingChange = false;
                    _useCachedRecordings = true;
                }
                catch (Exception)
                {
                }

                _semaphore.Release();
            }
        }

        return _useCachedRecordings;
    }

    private async Task<ChannelItemResult> GetRecordingGroups(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        List<ChannelItemInfo> pluginItems = new List<ChannelItemInfo>();

        if (await GetRecordingsAsync("GetRecordingGroups", cancellationToken))
        {
            var series = _allRecordings
                .Where(i => i.IsSeries)
                .ToLookup(i => i.Name, StringComparer.OrdinalIgnoreCase);

            pluginItems.AddRange(series.OrderBy(i => i.Key).Select(i => new ChannelItemInfo
            {
                Name = i.Key,
                FolderType = ChannelFolderType.Container,
                Id = "series_" + i.Key.GetMD5().ToString("N"),
                Type = ChannelItemType.Folder,
                DateCreated = i.Last().StartDate,
                ImageUrl = i.Last().ImageUrl.Replace("=poster", "=landscape", StringComparison.OrdinalIgnoreCase)
            }));

            var kids = _allRecordings.FirstOrDefault(i => i.IsKids);

            if (kids != null)
            {
                pluginItems.Add(new ChannelItemInfo
                {
                    Name = "Kids",
                    FolderType = ChannelFolderType.Container,
                    Id = "kids",
                    Type = ChannelItemType.Folder,
                    ImageUrl = kids.ImageUrl
                });
            }

            var movies = _allRecordings.FirstOrDefault(i => i.IsMovie);
            if (movies != null)
            {
                pluginItems.Add(new ChannelItemInfo
                {
                    Name = "Movies",
                    FolderType = ChannelFolderType.Container,
                    Id = "movies",
                    Type = ChannelItemType.Folder,
                    ImageUrl = movies.ImageUrl
                });
            }

            var news = _allRecordings.FirstOrDefault(i => i.IsNews);
            if (news != null)
            {
                pluginItems.Add(new ChannelItemInfo
                {
                    Name = "News",
                    FolderType = ChannelFolderType.Container,
                    Id = "news",
                    Type = ChannelItemType.Folder,
                    ImageUrl = news.ImageUrl
                });
            }

            var sports = _allRecordings.FirstOrDefault(i => i.IsSports);
            if (sports != null)
            {
                pluginItems.Add(new ChannelItemInfo
                {
                    Name = "Sports",
                    FolderType = ChannelFolderType.Container,
                    Id = "sports",
                    Type = ChannelItemType.Folder,
                    ImageUrl = sports.ImageUrl
                });
            }

            var other = _allRecordings.OrderByDescending(j => j.StartDate).FirstOrDefault(i => !i.IsSports && !i.IsNews && !i.IsMovie && !i.IsKids && !i.IsSeries);
            if (other != null)
            {
                pluginItems.Add(new ChannelItemInfo
                {
                    Name = "Others",
                    FolderType = ChannelFolderType.Container,
                    Id = "others",
                    Type = ChannelItemType.Folder,
                    DateModified = other.StartDate,
                    ImageUrl = other.ImageUrl
                });
            }
        }

        var result = new ChannelItemResult() { Items = pluginItems };
        return result;
    }

    private async void OnUpdateTimerCallbackAsync(object state)
    {
        LiveTvService service = LiveTvService.Instance;
        if (service is not null && service.IsActive)
        {
            var backendUpdate = await service.GetLastUpdate(_cancellationToken.Token).ConfigureAwait(false);
            if (backendUpdate > _lastUpdate)
            {
                _logger.LogDebug("Recordings reset {0}", backendUpdate);
                _useCachedRecordings = false;
                await GetRecordingsAsync("OnUpdateTimerCallbackAsync", _cancellationToken.Token);
            }
        }
    }

    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
    {
        throw new NotImplementedException();
    }
}
