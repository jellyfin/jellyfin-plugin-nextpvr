using System.Globalization;
using System.IO;
using MediaBrowser.Controller.LiveTv;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Serialization;
using NextPvr.Helpers;

namespace NextPvr.Responses
{
    public class SettingResponse
    {
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");

        public bool GetDefaultSettings(Stream stream, IJsonSerializer json, ILogger logger)
        {
            ScheduleSettings root = GetScheduleSettings(stream, json);
            UtilsHelper.DebugInformation(logger, string.Format("[NextPVR] GetDefaultTimerInfo Response: {0}", json.SerializeToString(root)));
            Plugin.Instance.Configuration.PostPaddingSeconds = int.Parse(root.postPadding) * 60;
            Plugin.Instance.Configuration.PrePaddingSeconds = int.Parse(root.prePadding) * 60;
            Plugin.Instance.Configuration.ShowRepeat = root.showNewInGuide;
            return true;
        }

        private ScheduleSettings GetScheduleSettings(Stream stream, IJsonSerializer json)
        {
            return json.DeserializeFromStream<ScheduleSettings>(stream);
        }

        public string GetSetting(Stream stream, IJsonSerializer json, ILogger logger)
        {
            SettingValue root = json.DeserializeFromStream<SettingValue>(stream);
            UtilsHelper.DebugInformation(logger, string.Format("[NextPVR] GetSetting Response: {0}", json.SerializeToString(root)));
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


