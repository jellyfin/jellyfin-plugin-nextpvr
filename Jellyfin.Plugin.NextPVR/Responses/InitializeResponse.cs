using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

/// <summary>
/// Reads the response to a session validation or login request.
/// </summary>
public class InitializeResponse
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    /// <summary>
    /// Determines whether the session is logged in.
    /// </summary>
    /// <param name="stream">The response stream to read.</param>
    /// <param name="logger">The logger to write diagnostic output to.</param>
    /// <returns><c>true</c> if the session is logged in; otherwise, <c>false</c>.</returns>
    public async Task<bool> LoggedIn(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(root.Stat))
        {
            UtilsHelper.DebugInformation(logger, $"Connection validation: {JsonSerializer.Serialize(root, _jsonOptions)}");
            return root.Stat == "ok";
        }

        logger.LogError("Failed to validate your connection with NextPVR");
        throw new JsonException("Failed to validate your connection with NextPVR.");
    }

    private sealed class RootObject
    {
        public string Stat { get; set; }

        public string Sid { get; set; }
    }
}
