using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.NextPVR.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextPVR.Responses;

public class SettingResponse
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Options;

    public async Task<bool> GetDefaultSettings(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<ScheduleSettings>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(logger, $"[NextPVR] GetDefaultTimerInfo Response: {JsonSerializer.Serialize(root, _jsonOptions)}");
        Plugin.Instance.Configuration.PostPaddingSeconds = int.Parse(root.PostPadding, CultureInfo.InvariantCulture) * 60;
        Plugin.Instance.Configuration.PrePaddingSeconds = int.Parse(root.PrePadding, CultureInfo.InvariantCulture) * 60;
        Plugin.Instance.Configuration.ShowRepeat = root.ShowNewInGuide;
        return true;
    }

    public async Task<string> GetSetting(Stream stream, ILogger<LiveTvService> logger)
    {
        var root = await JsonSerializer.DeserializeAsync<SettingValue>(stream, _jsonOptions).ConfigureAwait(false);
        UtilsHelper.DebugInformation(logger, $"[NextPVR] GetSetting Response: {JsonSerializer.Serialize(root, _jsonOptions)}");
        return root.Value;
    }

    // Classes created with http://json2csharp.com/

    private class ScheduleSettings
    {
        public string Version { get; set; }

        public string NextPvrVersion { get; set; }

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

        public string PrePadding { get; set; }

        public string PostPadding { get; set; }

        public bool ConfirmOnDelete { get; set; }

        public bool ShowNewInGuide { get; set; }

        public int SlipSeconds { get; set; }

        public string RecordingDirectories { get; set; }

        public bool ChannelDetailsLevel { get; set; }

        public string Time { get; set; }

        public int TimeEpoch { get; set; }
    }

    private class SettingValue
    {
        public string Value { get; set; }
    }
}
