using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.NextPVR.Helpers;
using MediaBrowser.Common.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.NextPVR.Responses
{
    public class InitializeResponse
    {
        private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.GetOptions();
        
        public async Task<bool> LoggedIn(Stream stream, ILogger<LiveTvService> logger)
        {
            var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);

            if (root.stat != "")
            {
                UtilsHelper.DebugInformation(logger, string.Format("[NextPVR] Connection validation: {0}", JsonSerializer.Serialize(root, _jsonOptions)));
                return root.stat == "ok";
            }
            logger.LogError("[NextPVR] Failed to validate your connection with NextPVR.");
            throw new Exception("Failed to validate your connection with NextPVR.");
        }

        public class RootObject
        {
            public string stat { get; set; }
            public string sid { get; set; }
        }
    }
}
