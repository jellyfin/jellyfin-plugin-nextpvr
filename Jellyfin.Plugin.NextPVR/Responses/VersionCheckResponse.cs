using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MediaBrowser.Common.Json;

namespace Jellyfin.Plugin.NextPVR.Responses
{
    public class VersionCheckResponse
    {
        private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.GetOptions();

        public async Task<bool> UpdateAvailable(Stream stream)
        {
            var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);
            if (root.versionCheck != null)
            {
                return root.versionCheck.upgradeAvailable;
            }

            throw new Exception("Failed to get the Update Status from NextPVR.");
        }

        public async Task<string> ServerVersion(Stream stream)
        {
            var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);
            if (root.versionCheck != null)
            {
                return root.versionCheck.serverVer;
            }

            throw new Exception("Failed to get the Server Version from NextPVR.");
        }

        public class VersionCheck
        {
            public bool upgradeAvailable { get; set; }
            public string onlineVer { get; set; }
            public string serverVer { get; set; }
        }

        public class RootObject
        {
            public VersionCheck versionCheck { get; set; }
        }
    }
}
