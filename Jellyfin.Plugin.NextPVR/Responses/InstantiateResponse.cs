using System;
using System.IO;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Serialization;
using NextPvr.Helpers;

namespace NextPvr.Responses
{
    public class InstantiateResponse
    {
        public ClientKeys GetClientKeys(Stream stream, IJsonSerializer json,ILogger<LiveTvService> logger)
        {
            var root = json.DeserializeFromStream<RootObject>(stream);

            if (root.clientKeys != null && root.clientKeys.sid != null && root.clientKeys.salt != null)
            {
                UtilsHelper.DebugInformation(logger,string.Format("[NextPVR] ClientKeys: {0}", json.SerializeToString(root)));
                return root.clientKeys;
            }

            logger.LogError("[NextPVR] Failed to load the ClientKeys from NextPVR.");
            throw new Exception("Failed to load the ClientKeys from NextPVR.");
        }

        public class ClientKeys
        {
            public string sid { get; set; }
            public string salt { get; set; }
        }

        private class RootObject
        {
            public ClientKeys clientKeys { get; set; }
        }
    }
}
