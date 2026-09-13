using System;
using System.Collections.Generic;
using Jellyfin.Plugin.NextPVR.Entities;

using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.NextPVR.Configuration;

/// <summary>
/// Class PluginConfiguration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        Pin = "0000";
        WebServiceUrl = "http://localhost:8866";
        EnableDebugLogging = false;
        NewEpisodes = false;
        RecordingDefault = "2";
        RecordingTransport = 1;
        EnableInProgress = false;
        PollInterval = 20;
        BackendVersion = 0;
        // Initialise this
        GenreMappings = new SerializableDictionary<string, List<string>>
        {
            ["GENRESPORT"] =
            [
                "Sports",
                "Football",
                "Baseball",
                "Basketball",
                "Hockey",
                "Soccer"
            ],
            ["GENRENEWS"] = ["News"],
            ["GENREKIDS"] = ["Kids", "Children"],
            ["GENREMOVIE"] = ["Movie", "Film"],
            ["GENRELIVE"] = ["Awards"]
        };
    }

    /// <summary>
    /// Gets or sets the URL of the NextPVR web service, as entered by the user.
    /// </summary>
    public string WebServiceUrl { get; set; }

    /// <summary>
    /// Gets or sets the web service URL that the current session was established with.
    /// </summary>
    public string CurrentWebServiceURL { get; set; }

    /// <summary>
    /// Gets or sets the version of the NextPVR backend.
    /// </summary>
    public int BackendVersion { get; set; }

    /// <summary>
    /// Gets or sets the PIN used to authenticate with NextPVR.
    /// </summary>
    public string Pin { get; set; }

    /// <summary>
    /// Gets or sets the session id kept from the last successful login, so that a
    /// session can be resumed without logging in again.
    /// </summary>
    public string StoredSid { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether additional debug logging is written.
    /// </summary>
    public bool EnableDebugLogging { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether recordings that are still in progress
    /// are presented as live streams.
    /// </summary>
    public bool EnableInProgress { get; set; }

    /// <summary>
    /// Gets or sets the interval, in seconds, at which the backend is polled for recording changes.
    /// </summary>
    public int PollInterval { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether series recordings are restricted to new episodes.
    /// </summary>
    public bool NewEpisodes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether NextPVR flags new episodes in the guide.
    /// </summary>
    public bool ShowRepeat { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether episode artwork is retrieved from the backend.
    /// </summary>
    public bool GetEpisodeImage { get; set; }

    /// <summary>
    /// Gets or sets the recurring type used by default when a series recording is created.
    /// </summary>
    public string RecordingDefault { get; set; }

    /// <summary>
    /// Gets or sets the transport used to play recordings back.
    /// </summary>
    public int RecordingTransport { get; set; }

    /// <summary>
    /// Gets or sets the number of seconds to start recording before a program begins.
    /// </summary>
    public int PrePaddingSeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of seconds to keep recording after a program ends.
    /// </summary>
    public int PostPaddingSeconds { get; set; }

    /// <summary>
    /// Gets or sets the time at which the recording list last changed.
    /// </summary>
    public DateTime RecordingModificationTime { get; set; }

    /// <summary>
    /// Gets or sets the genre mappings, to map localised NextPVR genres, to Jellyfin categories.
    /// </summary>
    // The setter is required for the XML deserialization of the plugin configuration.
#pragma warning disable CA2227
    public SerializableDictionary<string, List<string>> GenreMappings { get; set; }
#pragma warning restore CA2227
}
