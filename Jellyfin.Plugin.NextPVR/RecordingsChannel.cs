using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.NextPVR.Entities;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.MediaInfo;

namespace Jellyfin.Plugin.NextPVR;

public class RecordingsChannel : IChannel, IHasCacheKey, ISupportsDelete, ISupportsLatestMedia, ISupportsMediaProbe, IHasFolderAttributes, IDisposable
{
    private readonly CancellationTokenSource _cancellationToken;
    private Timer _updateTimer;
    private DateTimeOffset _lastUpdate = DateTimeOffset.FromUnixTimeSeconds(0);

    private IEnumerable<MyRecordingInfo> _allRecordings;
    private bool _useCachedRecordings;

    public RecordingsChannel()
    {
        var interval = TimeSpan.FromSeconds(20);
        _updateTimer = new Timer(OnUpdateTimerCallbackAsync, null, interval, interval);
        if (_updateTimer != null)
        {
            _cancellationToken = new CancellationTokenSource();
        }
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
        var now = DateTime.UtcNow;

        var values = new List<string>();

        values.Add(now.DayOfYear.ToString(CultureInfo.InvariantCulture));
        values.Add(now.Hour.ToString(CultureInfo.InvariantCulture));

        double minute = now.Minute;
        minute /= 5;

        values.Add(Math.Floor(minute).ToString(CultureInfo.InvariantCulture));

        values.Add(GetService().LastRecordingChange.Ticks.ToString(CultureInfo.InvariantCulture));

        return string.Join('-', values.ToArray());
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
        if (service is not null && !service.IsActive)
        {
            service.EnsureConnectionAsync(new System.Threading.CancellationToken(false)).Wait();
        }

        return service;
    }

    public bool CanDelete(BaseItem item)
    {
        return !item.IsFolder;
    }

    public Task DeleteItem(string id, CancellationToken cancellationToken)
    {
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

    public Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.FolderId))
        {
            return GetRecordingGroups(query, cancellationToken);
        }

        if (query.FolderId.StartsWith("series_", StringComparison.OrdinalIgnoreCase))
        {
            var hash = query.FolderId.Split('_')[1];
            return GetChannelItems(query, i => i.IsSeries && string.Equals(i.Name.GetMD5().ToString("N"), hash, StringComparison.Ordinal), cancellationToken);
        }

        if (string.Equals(query.FolderId, "kids", StringComparison.OrdinalIgnoreCase))
        {
            return GetChannelItems(query, i => i.IsKids, cancellationToken);
        }

        if (string.Equals(query.FolderId, "movies", StringComparison.OrdinalIgnoreCase))
        {
            return GetChannelItems(query, i => i.IsMovie, cancellationToken);
        }

        if (string.Equals(query.FolderId, "news", StringComparison.OrdinalIgnoreCase))
        {
            return GetChannelItems(query, i => i.IsNews, cancellationToken);
        }

        if (string.Equals(query.FolderId, "sports", StringComparison.OrdinalIgnoreCase))
        {
            return GetChannelItems(query, i => i.IsSports, cancellationToken);
        }

        if (string.Equals(query.FolderId, "others", StringComparison.OrdinalIgnoreCase))
        {
            return GetChannelItems(query, i => !i.IsSports && !i.IsNews && !i.IsMovie && !i.IsKids && !i.IsSeries, cancellationToken);
        }

        var result = new ChannelItemResult() { Items = new List<ChannelItemInfo>() };

        return Task.FromResult(result);
    }

    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, Func<MyRecordingInfo, bool> filter, CancellationToken cancellationToken)
    {
        var service = GetService();
        if (_useCachedRecordings == false)
        {
            _allRecordings = await service.GetAllRecordingsAsync(cancellationToken).ConfigureAwait(false);

            await service.GetChannelsAsync(cancellationToken).ConfigureAwait(false);

            _useCachedRecordings = true;
        }

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
            OfficialRating = item.OfficialRating,
            CommunityRating = item.CommunityRating,
            ContentType = item.IsMovie ? ChannelMediaContentType.Movie : (item.IsSeries ? ChannelMediaContentType.Episode : ChannelMediaContentType.Clip),
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
                    Protocol = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? MediaProtocol.Http : MediaProtocol.File,
                    Id = item.Id,
                    IsInfiniteStream = item.Status == RecordingStatus.InProgress,
                    RunTimeTicks = (item.EndDate - item.StartDate).Ticks,
                }
            },
            PremiereDate = item.OriginalAirDate,
            ProductionYear = item.ProductionYear,
            Type = ChannelItemType.Media,
            DateModified = item.DateLastUpdated,
            Overview = item.Overview,
            IsLiveStream = item.Status == RecordingStatus.InProgress,
            Etag = item.Status.ToString()
        };

        return channelItem;
    }

    private async Task<ChannelItemResult> GetRecordingGroups(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        List<ChannelItemInfo> pluginItems = new List<ChannelItemInfo>();
        var service = GetService();
        if (_useCachedRecordings == false)
        {
            _allRecordings = await service.GetAllRecordingsAsync(cancellationToken).ConfigureAwait(false);
            await service.GetChannelsAsync(cancellationToken).ConfigureAwait(false);
            _useCachedRecordings = true;
        }

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

        var other = _allRecordings.FirstOrDefault(i => !i.IsSports && !i.IsNews && !i.IsMovie && !i.IsKids && !i.IsSeries);
        if (other != null)
        {
            pluginItems.Add(new ChannelItemInfo
            {
                Name = "Others",
                FolderType = ChannelFolderType.Container,
                Id = "others",
                Type = ChannelItemType.Folder,
                ImageUrl = other.ImageUrl
            });
        }

        var result = new ChannelItemResult() { Items = pluginItems };
        return result;
    }

    private async void OnUpdateTimerCallbackAsync(object state)
    {
        var service = GetService();
        if (service is not null && service.IsActive)
        {
            var backendUpdate = await service.GetLastUpdate(_cancellationToken.Token).ConfigureAwait(false);
            if (backendUpdate > _lastUpdate)
            {
                _useCachedRecordings = false;
                _lastUpdate = backendUpdate;
            }
        }
    }
}
