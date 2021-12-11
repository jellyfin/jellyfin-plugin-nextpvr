using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Entities;
using Jellyfin.Plugin.NextPVR.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

public class InstantiateResponse
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Options;

    public async Task<ClientKeys> GetClientKeys(Stream stream, ILogger<LiveTvService> logger)
    {
        try
        {
            var root = await JsonSerializer.DeserializeAsync<ClientKeys>(stream, _jsonOptions).ConfigureAwait(false);

            if (root.Sid != null && root.Salt != null)
            {
                UtilsHelper.DebugInformation(logger, $"[NextPVR] ClientKeys: {JsonSerializer.Serialize(root, _jsonOptions)}");
                return root;
            }

            logger.LogError("[NextPVR] Failed to validate the ClientKeys from NextPVR");
            throw new JsonException("Failed to load the ClientKeys from NextPVR.");
        }
        catch
        {
            logger.LogError("Check NextPVR Version 5");
            throw new UnauthorizedAccessException("Check NextPVR Version");
        }
    }
}
