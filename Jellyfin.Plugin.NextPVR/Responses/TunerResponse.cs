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
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Options;

    public async Task<List<LiveTvTunerInfo>> LiveTvTunerInfos(Stream stream)
    {
        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);
        return root.Tuners.Select(GetTunerInformation).ToList();
    }

    private LiveTvTunerInfo GetTunerInformation(Tuner i)
    {
        LiveTvTunerInfo tunerinfo = new LiveTvTunerInfo();

        tunerinfo.Name = i.TunerName;
        tunerinfo.Status = GetStatus(i);

        if (i.Recordings.Count > 0)
        {
            tunerinfo.ChannelId = i.Recordings.Single().Recording.ChannelOid.ToString(CultureInfo.InvariantCulture);
        }

        return tunerinfo;
    }

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

    private class Recording
    {
        public int TunerOid { get; set; }

        public string RecName { get; set; }

        public int ChannelOid { get; set; }

        public int RecordingOid { get; set; }
    }

    private class Recordings
    {
        public Recording Recording { get; set; }
    }

    private class Tuner
    {
        public string TunerName { get; set; }

        public string TunerStatus { get; set; }

        public List<Recordings> Recordings { get; set; }

        public List<object> LiveTv { get; set; }
    }

    private class RootObject
    {
        public List<Tuner> Tuners { get; set; }
    }
}
