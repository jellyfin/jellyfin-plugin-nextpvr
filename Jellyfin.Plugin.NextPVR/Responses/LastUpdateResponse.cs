using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

/// <summary>
/// Reads the response describing when the recordings last changed.
/// </summary>
public class LastUpdateResponse
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    /// <summary>
    /// Reads the time at which the backend recordings last changed.
    /// </summary>
    /// <param name="stream">The response stream to read.</param>
    /// <param name="logger">The logger to write diagnostic output to.</param>
    /// <returns>The time of the last change.</returns>
    public async Task<DateTimeOffset> GetUpdateTime(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(logger, $"LastUpdate Response: {JsonSerializer.Serialize(root, _jsonOptions)}");

        if (root is null)
        {
            logger.LogError("Failed to read the last update time");
            throw new JsonException("Failed to read the last update time.");
        }

        return DateTimeOffset.FromUnixTimeSeconds(root.LastUpdate);
    }

    private sealed class RootObject
    {
        [JsonPropertyName("last_update")]
        public int LastUpdate { get; set; }

        public string? Stat { get; set; }

        public int Code { get; set; }

        public string? Msg { get; set; }
    }
}
