using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;

namespace Jellyfin.Plugin.NextPVR.Responses;

public class TunerResponse
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    public async Task<List<TunerHostInfo>> LiveTvTunerInfo(Stream stream)
    {
        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);
        return root.Tuners.Select(GetTunerInformation).ToList();
    }

    private TunerHostInfo GetTunerInformation(Tuner i)
    {
        TunerHostInfo tunerinfo = new TunerHostInfo();

        tunerinfo.FriendlyName = i.TunerName;
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

    private sealed class Recording
    {
        public int TunerOid { get; set; }

        public string RecName { get; set; }

        public int ChannelOid { get; set; }

        public int RecordingOid { get; set; }
    }

    private sealed class Recordings
    {
        public Recording Recording { get; set; }
    }

    private sealed class Tuner
    {
        public string TunerName { get; set; }

        public string TunerStatus { get; set; }

        public List<Recordings> Recordings { get; set; }

        public List<object> LiveTv { get; set; }
    }

    private sealed class RootObject
    {
        public List<Tuner> Tuners { get; set; }
    }
}
