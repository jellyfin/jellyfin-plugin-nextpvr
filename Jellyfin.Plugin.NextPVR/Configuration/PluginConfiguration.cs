using System;
using System.Collections.Generic;
using NextPvr.Entities;


using MediaBrowser.Model.Plugins;

namespace NextPvr.Configuration
{
    /// <summary>
    /// Class PluginConfiguration
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string WebServiceUrl { get; set; }

        public string Pin { get; set; }

        public Boolean EnableDebugLogging { get; set; }
        public Boolean NewEpisodes { get; set; }
        public bool ShowRepeat { get; set; }
        public bool GetEpisodeImage { get; set; }
        public string RecordingDefault { get; set; }
        public int PrePaddingSeconds { get; set; }
        public int PostPaddingSeconds { get; set; }
/// <summary>
/// The genre mappings, to map localised NextPVR genres, to Emby categories.
/// </summary>
public SerializableDictionary<String, List<String>> GenreMappings { get; set; }

        public PluginConfiguration()
        {
            Pin = "0000";
            WebServiceUrl = "http://localhost:8866";
            EnableDebugLogging = false;
            NewEpisodes = false;
            RecordingDefault = "2";
            // Initialise this
            GenreMappings = new SerializableDictionary<string, List<string>>();
            GenreMappings["GENRESPORT"] =  new List<string>() { "Sports", "Football", "Baseball", "Basketball", "Hockey", "Soccer" };
            GenreMappings["GENRENEWS"] = new List<string>() { "News" };
            GenreMappings["GENREKIDS"] = new List<string>() { "Kids", "Children" };
            GenreMappings["GENREMOVIE"] = new List<string>() { "Movie", "Film" };
            GenreMappings["GENRELIVE"] = new List<string>() { "Awards" };
        }
    }
}
