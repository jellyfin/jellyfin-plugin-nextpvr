using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.NextPVR.Configuration;
using Jellyfin.Plugin.NextPVR.Entities;
using MediaBrowser.Controller.LiveTv;

namespace Jellyfin.Plugin.NextPVR.Helpers;

/// <summary>
/// Provides methods to map MediaPortal genres to Emby categories.
/// </summary>
public class GenreMapper
{
    private const string GenreMovie = "GENREMOVIE";
    private const string GenreSport = "GENRESPORT";
    private const string GenreNews = "GENRENEWS";
    private const string GenreKids = "GENREKIDS";
    private const string GenreLive = "GENRELIVE";

    private readonly PluginConfiguration _configuration;
    private readonly List<string> _movieGenres;
    private readonly List<string> _sportGenres;
    private readonly List<string> _newsGenres;
    private readonly List<string> _kidsGenres;
    private readonly List<string> _liveGenres;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenreMapper"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public GenreMapper(PluginConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _movieGenres = new List<string>();
        _sportGenres = new List<string>();
        _newsGenres = new List<string>();
        _kidsGenres = new List<string>();
        _liveGenres = new List<string>();
        LoadInternalLists(_configuration.GenreMappings);
    }

    private void LoadInternalLists(Dictionary<string, List<string>> genreMappings)
    {
        if (genreMappings != null)
        {
            if (_configuration.GenreMappings.ContainsKey(GenreMovie) && _configuration.GenreMappings[GenreMovie] != null)
            {
                _movieGenres.AddRange(_configuration.GenreMappings[GenreMovie]);
            }

            if (_configuration.GenreMappings.ContainsKey(GenreSport) && _configuration.GenreMappings[GenreSport] != null)
            {
                _sportGenres.AddRange(_configuration.GenreMappings[GenreSport]);
            }

            if (_configuration.GenreMappings.ContainsKey(GenreNews) && _configuration.GenreMappings[GenreNews] != null)
            {
                _newsGenres.AddRange(_configuration.GenreMappings[GenreNews]);
            }

            if (_configuration.GenreMappings.ContainsKey(GenreKids) && _configuration.GenreMappings[GenreKids] != null)
            {
                _kidsGenres.AddRange(_configuration.GenreMappings[GenreKids]);
            }

            if (_configuration.GenreMappings.ContainsKey(GenreLive) && _configuration.GenreMappings[GenreLive] != null)
            {
                _liveGenres.AddRange(_configuration.GenreMappings[GenreLive]);
            }
        }
    }

    /// <summary>
    /// Populates the program genres.
    /// </summary>
    /// <param name="program">The program.</param>
    public void PopulateProgramGenres(ProgramInfo program)
    {
        // Check there is a program and genres to map
        if (program?.Genres != null && program.Genres.Count > 0)
        {
            program.IsMovie = _movieGenres.Any(g => program.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            program.IsSports = _sportGenres.Any(g => program.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            program.IsNews = _newsGenres.Any(g => program.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            program.IsKids = _kidsGenres.Any(g => program.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            if (program.IsLive == false)
            {
                program.IsLive = _liveGenres.Any(g => program.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            }
        }
    }

    /// <summary>
    /// Populates the recording genres.
    /// </summary>
    /// <param name="recording">The recording.</param>
    public void PopulateRecordingGenres(MyRecordingInfo recording)
    {
        // Check there is a recording and genres to map
        if (recording?.Genres != null && recording.Genres.Count > 0)
        {
            recording.IsMovie = _movieGenres.Any(g => recording.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            recording.IsSports = _sportGenres.Any(g => recording.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            recording.IsNews = _newsGenres.Any(g => recording.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            recording.IsKids = _kidsGenres.Any(g => recording.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            recording.IsLive = _liveGenres.Any(g => recording.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
        }
    }

    /// <summary>
    /// Populates the timer genres.
    /// </summary>
    /// <param name="timer">The timer.</param>
    public void PopulateTimerGenres(TimerInfo timer)
    {
        // Check there is a timer and genres to map
        if (timer?.Genres != null && timer.Genres.Length > 0)
        {
            timer.IsMovie = _movieGenres.Any(g => timer.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            // timer.IsSports = _sportGenres.Any(g => timer.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            // timer.IsNews = _newsGenres.Any(g => timer.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            // timer.IsKids = _kidsGenres.Any(g => timer.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
            // timer.IsProgramSeries = _seriesGenres.Any(g => timer.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
        }
    }
}
