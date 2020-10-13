using System;
using System.IO;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.NextPVR.Helpers;

namespace Jellyfin.Plugin.NextPVR.Responses
{
    public class InstantiateResponse
    {
        public ClientKeys GetClientKeys(Stream stream, IJsonSerializer json, ILogger<LiveTvService> logger)
        {
            try
            {
                var root = json.DeserializeFromStream<ClientKeys>(stream);

                if (root.sid != null && root.salt != null)
                {
                    UtilsHelper.DebugInformation(logger, string.Format("[NextPVR] ClientKeys: {0}", json.SerializeToString(root)));
                    return root;
                }
                logger.LogError("[NextPVR] Failed to validate the ClientKeys from NextPVR.");
                throw new Exception("Failed to load the ClientKeys from NextPVR.");
            }
            catch
            {
                logger.LogError("Check NextPVR Version 5");
                throw new UnauthorizedAccessException("Check NextPVR Version");
            }
        }

        public class ClientKeys
        {
            public string sid { get; set; }
            public string salt { get; set; }
        }
    }
}
