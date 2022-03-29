using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

public class LastUpdateResponse
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    public async Task<DateTimeOffset> GetUpdateTime(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(logger, $"[NextPVR] LastUpdate Response: {JsonSerializer.Serialize(root, _jsonOptions)}");
        return DateTimeOffset.FromUnixTimeSeconds(root.LastUpdate);
    }
}

// Classes created with http://json2csharp.com/

public class RootObject
{
    public int LastUpdate { get; set; }

    public string Stat { get; set; }

    public int Code { get; set; }

    public string Msg { get; set; }
}
