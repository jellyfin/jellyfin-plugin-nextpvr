using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.NextPVR.Helpers;
using System.Text.Json;
using MediaBrowser.Common.Json;

namespace Jellyfin.Plugin.NextPVR.Responses
{
    public class CancelDeleteRecordingResponse
    {
        private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.GetOptions();

        public async Task<bool?> RecordingError(Stream stream, ILogger<LiveTvService> logger)
        {
            var root = await JsonSerializer.DeserializeAsync<RootObject>(stream, _jsonOptions).ConfigureAwait(false);

            if (root.stat != "ok")
            {
                UtilsHelper.DebugInformation(logger, string.Format("[NextPVR] RecordingError Response: {0}", JsonSerializer.Serialize(root, _jsonOptions)));
                return true;
            }
            return false;
        }

        public class RootObject
        {
            public string stat { get; set; }
        }
    }
}
