using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

/// <summary>
/// Reads the response to a request that cancels or deletes a recording.
/// </summary>
public class CancelDeleteRecordingResponse
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    /// <summary>
    /// Determines whether the request reported an error.
    /// </summary>
    /// <param name="stream">The response stream to read.</param>
    /// <param name="logger">The logger to write diagnostic output to.</param>
    /// <returns><c>true</c> if the request failed, <c>false</c> if it succeeded, or <c>null</c> if the response was empty.</returns>
    public async Task<bool?> RecordingError(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);

        if (root is null)
        {
            logger.LogError("Failed to read the recording response");
            return null;
        }

        if (root.Stat != "ok")
        {
            UtilsHelper.DebugInformation(logger, $"RecordingError Response: {JsonSerializer.Serialize(root, _jsonOptions)}");
            return true;
        }

        return false;
    }

    private sealed class RootObject
    {
        public string? Stat { get; set; }
    }
}
