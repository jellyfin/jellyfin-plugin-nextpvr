using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

public class CancelDeleteRecordingResponse
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    public async Task<bool?> RecordingError(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);

        if (root.Stat != "ok")
        {
            UtilsHelper.DebugInformation(logger, $"[NextPVR] RecordingError Response: {JsonSerializer.Serialize(root, _jsonOptions)}");
            return true;
        }

        return false;
    }

    private sealed class RootObject
    {
        public string Stat { get; set; }
    }
}
