using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Responses.Dto;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;

namespace Jellyfin.Plugin.NextPVR.Responses;

/// <summary>
/// Reads the response to a tuner listing request.
/// </summary>
public class TunerResponse
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    /// <summary>
    /// Reads the tuners reported by the backend.
    /// </summary>
    /// <param name="stream">The response stream to read.</param>
    /// <returns>The available tuners.</returns>
    public async Task<List<TunerHostInfo>> LiveTvTunerInfo(Stream stream)
    {
        var root = await JsonSerializer.DeserializeAsync<TunerRoot>(stream, _jsonOptions).ConfigureAwait(false);

        if (root?.Tuners is null)
        {
            throw new JsonException("Failed to download the tuner information.");
        }

        return root.Tuners.Select(GetTunerInformation).ToList();
    }

    private TunerHostInfo GetTunerInformation(Tuner i)
    {
        TunerHostInfo tunerinfo = new TunerHostInfo
        {
            FriendlyName = i.TunerName
        };

        /*
        tunerinfo.Status = GetStatus(i);

        if (i.Recordings.Count > 0)
        {
            tunerinfo.ChannelId = i.Recordings.Single().Recording.ChannelOid.ToString(CultureInfo.InvariantCulture);
        }
        */
        return tunerinfo;
    }

    /*
    private LiveTvTunerStatus GetStatus(Tuner i)
    {
        if (i.Recordings.Count > 0)
        {
            return LiveTvTunerStatus.RecordingTv;
        }

        if (i.LiveTv.Count > 0)
        {
            return LiveTvTunerStatus.LiveTv;
        }

        return LiveTvTunerStatus.Available;
    }
    */
}
