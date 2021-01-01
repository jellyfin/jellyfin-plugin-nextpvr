using System.Globalization;
using System.IO;
using System;
using MediaBrowser.Controller.LiveTv;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.NextPVR.Helpers;
using System.Threading.Tasks;
using MediaBrowser.Common.Json;
using System.Text.Json;

namespace Jellyfin.Plugin.NextPVR.Responses
{
    public class LastUpdateResponse
    {
        public async Task<DateTimeOffset> GetUpdateTime(Stream stream, ILogger<LiveTvService> logger)
        {
            var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, JsonDefaults.GetOptions()).ConfigureAwait(false);
            UtilsHelper.DebugInformation(logger, string.Format("[NextPVR] LastUpdate Response: {0}", JsonSerializer.Serialize(root, JsonDefaults.GetOptions())));
            return DateTimeOffset.FromUnixTimeSeconds(root.last_update);
        }
    }

    // Classes created with http://json2csharp.com/

    public class RootObject
    {
        public int last_update { get; set; }
        public string stat { get; set; }
        public int code { get; set; }
        public string msg { get; set; }
    }
}
