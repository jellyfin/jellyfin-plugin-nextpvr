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
        GenreMappings = new SerializableDictionary<string, List<string>>();
        GenreMappings["GENRESPORT"] = new List<string>()
        {
            "Sports",
            "Football",
            "Baseball",
            "Basketball",
            "Hockey",
            "Soccer"
        };
        GenreMappings["GENRENEWS"] = new List<string>() { "News" };
        GenreMappings["GENREKIDS"] = new List<string>() { "Kids", "Children" };
        GenreMappings["GENREMOVIE"] = new List<string>() { "Movie", "Film" };
        GenreMappings["GENRELIVE"] = new List<string>() { "Awards" };
    }

    public string WebServiceUrl { get; set; }

    public string CurrentWebServiceURL { get; set; }

    public int BackendVersion { get; set; }

    public string Pin { get; set; }

    public string StoredSid { get; set; }

    public bool EnableDebugLogging { get; set; }

    public bool EnableInProgress { get; set; }

    public int PollInterval { get; set; }

    public bool NewEpisodes { get; set; }

    public bool ShowRepeat { get; set; }

    public bool GetEpisodeImage { get; set; }

    public string RecordingDefault { get; set; }

    public int RecordingTransport { get; set; }

    public int PrePaddingSeconds { get; set; }

    public int PostPaddingSeconds { get; set; }

    public DateTime RecordingModificationTime { get; set; }

    /// <summary>
    /// Gets or sets the genre mappings, to map localised NextPVR genres, to Jellyfin categories.
    /// </summary>
    public SerializableDictionary<string, List<string>> GenreMappings { get; set; }
}
