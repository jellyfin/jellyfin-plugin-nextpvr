using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

public class InitializeResponse
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    public async Task<bool> LoggedIn(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(root.Stat))
        {
            UtilsHelper.DebugInformation(logger, $"[NextPVR] Connection validation: {JsonSerializer.Serialize(root, _jsonOptions)}");
            return root.Stat == "ok";
        }

        logger.LogError("[NextPVR] Failed to validate your connection with NextPVR");
        throw new JsonException("Failed to validate your connection with NextPVR.");
    }

    private class RootObject
    {
        public string Stat { get; set; }

        public string Sid { get; set; }
    }
}
