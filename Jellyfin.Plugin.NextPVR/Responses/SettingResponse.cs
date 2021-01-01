using System.Globalization;
using System.IO;
using MediaBrowser.Controller.LiveTv;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.NextPVR.Helpers;
using System.Threading.Tasks;
using System.Text.Json;
using MediaBrowser.Common.Json;

namespace Jellyfin.Plugin.NextPVR.Responses
{
    public class SettingResponse
    {
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");

        public async Task<bool> GetDefaultSettings(Stream stream, ILogger<LiveTvService> logger)
        {
            var root = await JsonSerializer.DeserializeAsync<ScheduleSettings>(stream, JsonDefaults.GetOptions()).ConfigureAwait(false);
            UtilsHelper.DebugInformation(logger, string.Format("[NextPVR] GetDefaultTimerInfo Response: {0}", JsonSerializer.Serialize(root, JsonDefaults.GetOptions())));
            Plugin.Instance.Configuration.PostPaddingSeconds = int.Parse(root.postPadding) * 60;
            Plugin.Instance.Configuration.PrePaddingSeconds = int.Parse(root.prePadding) * 60;
            Plugin.Instance.Configuration.ShowRepeat = root.showNewInGuide;
            return true;
        }

        public async Task<string> GetSetting(Stream stream, ILogger<LiveTvService> logger)
        {
            var root = await JsonSerializer.DeserializeAsync<SettingValue>(stream, JsonDefaults.GetOptions()).ConfigureAwait(false);
            UtilsHelper.DebugInformation(logger, string.Format("[NextPVR] GetSetting Response: {0}", JsonSerializer.Serialize(root, JsonDefaults.GetOptions())));
            return root.value;
        }

        // Classes created with http://json2csharp.com/

        private class ScheduleSettings
        {
            public string version { get; set; }
            public string nextPVRVersion { get; set; }
            public string readableVersion { get; set; }
            public bool liveTimeshift { get; set; }
            public bool liveTimeshiftBufferInfo { get; set; }
            public bool channelsUseSegmenter { get; set; }
            public bool recordingsUseSegmenter { get; set; }
            public int whatsNewDays { get; set; }
            public int skipForwardSeconds { get; set; }
            public int skipBackSeconds { get; set; }
            public int skipFFSeconds { get; set; }
            public int skipRWSeconds { get; set; }
            public string recordingView { get; set; }
            public string prePadding { get; set; }
            public string postPadding { get; set; }
            public bool confirmOnDelete { get; set; }
            public bool showNewInGuide { get; set; }
            public int slipSeconds { get; set; }
            public string recordingDirectories { get; set; }
            public bool channelDetailsLevel { get; set; }
            public string time { get; set; }
            public int timeEpoch { get; set; }
        }

        private class SettingValue
        {
            public string value { get; set; }
        }
    }
}
