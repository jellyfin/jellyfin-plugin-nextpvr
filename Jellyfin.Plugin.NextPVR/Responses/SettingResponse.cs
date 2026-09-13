using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Helpers;
using Jellyfin.Plugin.NextPVR.Responses.Dto;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

/// <summary>
/// Reads the responses to backend setting requests.
/// </summary>
public class SettingResponse
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    /// <summary>
    /// Reads the backend scheduling defaults and copies them into the plugin configuration.
    /// </summary>
    /// <param name="stream">The response stream to read.</param>
    /// <param name="logger">The logger to write diagnostic output to.</param>
    /// <returns>Always <c>true</c>.</returns>
    public async Task<bool> GetDefaultSettings(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<ScheduleSettings>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(logger, $"GetDefaultTimerInfo Response: {JsonSerializer.Serialize(root, _jsonOptions)}");

        if (root is null)
        {
            logger.LogError("Failed to download the backend settings");
            throw new JsonException("Failed to download the backend settings.");
        }

        Plugin.Instance.Configuration.PostPaddingSeconds = root.PostPadding;
        Plugin.Instance.Configuration.PrePaddingSeconds = root.PrePadding;
        Plugin.Instance.Configuration.ShowRepeat = root.ShowNewInGuide;
        Plugin.Instance.Configuration.BackendVersion = root.NextPvrVersion;
        return true;
    }

    /// <summary>
    /// Reads the value of a single backend setting.
    /// </summary>
    /// <param name="stream">The response stream to read.</param>
    /// <param name="logger">The logger to write diagnostic output to.</param>
    /// <returns>The value of the setting.</returns>
    public async Task<string> GetSetting(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<SettingValue>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(logger, $"GetSetting Response: {JsonSerializer.Serialize(root, _jsonOptions)}");

        if (root is null)
        {
            logger.LogError("Failed to download the backend setting");
            throw new JsonException("Failed to download the backend setting.");
        }

        return root.Value ?? string.Empty;
    }

    // Classes created with http://json2csharp.com/
}
