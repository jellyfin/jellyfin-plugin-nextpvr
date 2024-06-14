using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

public class SettingResponse
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.CamelCaseOptions;

    public async Task<bool> GetDefaultSettings(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<ScheduleSettings>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(logger, $"GetDefaultTimerInfo Response: {JsonSerializer.Serialize(root, _jsonOptions)}");
        Plugin.Instance.Configuration.PostPaddingSeconds = root.PostPadding;
        Plugin.Instance.Configuration.PrePaddingSeconds = root.PrePadding;
        Plugin.Instance.Configuration.ShowRepeat = root.ShowNewInGuide;
        Plugin.Instance.Configuration.BackendVersion = root.NextPvrVersion;
        return true;
    }

    public async Task<string> GetSetting(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<SettingValue>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(logger, $"GetSetting Response: {JsonSerializer.Serialize(root, _jsonOptions)}");
        return root.Value;
    }

    // Classes created with http://json2csharp.com/

    private sealed class ScheduleSettings
    {
        public string Version { get; set; }

        [JsonPropertyName("nextPVRVersion")]
        public int NextPvrVersion { get; set; }

        public string ReadableVersion { get; set; }

        public bool LiveTimeshift { get; set; }

        public bool LiveTimeshiftBufferInfo { get; set; }

        public bool ChannelsUseSegmenter { get; set; }

        public bool RecordingsUseSegmenter { get; set; }

        public int WhatsNewDays { get; set; }

        public int SkipForwardSeconds { get; set; }

        public int SkipBackSeconds { get; set; }

        public int SkipFfSeconds { get; set; }

        public int SkipRwSeconds { get; set; }

        public string RecordingView { get; set; }

        public int PrePadding { get; set; }

        public int PostPadding { get; set; }

        public bool ConfirmOnDelete { get; set; }

        public bool ShowNewInGuide { get; set; }

        public int SlipSeconds { get; set; }

        public string RecordingDirectories { get; set; }

        public bool ChannelDetailsLevel { get; set; }

        public string Time { get; set; }

        public int TimeEpoch { get; set; }
    }

    private sealed class SettingValue
    {
        public string Value { get; set; }
    }
}
