using System;
using System.Collections.Generic;
using System.Linq;

using MediaBrowser.Controller.LiveTv;
using NextPvr.Configuration;


namespace NextPvr.Helpers
{
    /// <summary>
    /// Provides methods to map MediaPortal genres to Emby categories
    /// </summary>
    public class GenreMapper
    {
        public const string GENRE_MOVIE = "GENREMOVIE";
        public const string GENRE_SPORT = "GENRESPORT";
        public const string GENRE_NEWS = "GENRENEWS";
        public const string GENRE_KIDS = "GENREKIDS";
        public const string GENRE_LIVE = "GENRELIVE";

        private readonly PluginConfiguration _configuration;
        private readonly List<String> _movieGenres;
        private readonly List<String> _sportGenres;
        private readonly List<String> _newsGenres;
        private readonly List<String> _kidsGenres;
        private readonly List<String> _liveGenres;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenreMapper"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public GenreMapper(PluginConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException("configuration");

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
                if (_configuration.GenreMappings.ContainsKey(GENRE_MOVIE) && _configuration.GenreMappings[GENRE_MOVIE] != null)
                {
                    _movieGenres.AddRange(_configuration.GenreMappings[GENRE_MOVIE]);
                }

                if (_configuration.GenreMappings.ContainsKey(GENRE_SPORT) && _configuration.GenreMappings[GENRE_SPORT] != null)
                {
                    _sportGenres.AddRange(_configuration.GenreMappings[GENRE_SPORT]);
                }

                if (_configuration.GenreMappings.ContainsKey(GENRE_NEWS) && _configuration.GenreMappings[GENRE_NEWS] != null)
                {
                    _newsGenres.AddRange(_configuration.GenreMappings[GENRE_NEWS]);
                }

                if (_configuration.GenreMappings.ContainsKey(GENRE_KIDS) && _configuration.GenreMappings[GENRE_KIDS] != null)
                {
                    _kidsGenres.AddRange(_configuration.GenreMappings[GENRE_KIDS]);
                }

                if (_configuration.GenreMappings.ContainsKey(GENRE_LIVE) && _configuration.GenreMappings[GENRE_LIVE] != null)
                {
                    _liveGenres.AddRange(_configuration.GenreMappings[GENRE_LIVE]);
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
            if (program != null)
            {
                if (program.Genres != null && program.Genres.Count > 0)
                {
                    program.IsMovie = _movieGenres.Any(g => program.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                    program.IsSports = _sportGenres.Any(g => program.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                    program.IsNews = _newsGenres.Any(g => program.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                    program.IsKids = _kidsGenres.Any(g => program.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                    if (program.IsLive == false)
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
            if (recording != null)
            {
                if (recording.Genres != null && recording.Genres.Count > 0)
                {
                    recording.IsMovie = _movieGenres.Any(g => recording.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                    recording.IsSports = _sportGenres.Any(g => recording.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                    recording.IsNews = _newsGenres.Any(g => recording.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                    recording.IsKids = _kidsGenres.Any(g => recording.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                    recording.IsLive = _liveGenres.Any(g => recording.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                }
            }
        }

        /// <summary>
        /// Populates the timer genres.
        /// </summary>
        /// <param name="recording">The timer.</param>
        public void PopulateTimerGenres(TimerInfo timer)
        {
            // Check there is a timer and genres to map
            if (timer != null)
            {
                if (timer.Genres != null && timer.Genres.Length > 0)
                {
                    timer.IsMovie = _movieGenres.Any(g => timer.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                    //timer.IsSports = _sportGenres.Any(g => timer.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                    //timer.IsNews = _newsGenres.Any(g => timer.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                    //timer.IsKids = _kidsGenres.Any(g => timer.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                    //timer.IsProgramSeries = _seriesGenres.Any(g => timer.Genres.Contains(g, StringComparer.InvariantCultureIgnoreCase));
                }
            }
        }
    }
}
