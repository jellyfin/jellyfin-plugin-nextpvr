const NextPvrConfigurationPage = {
    pluginUniqueId: '9574ac10-bf23-49bc-949f-924f23cfa48f'
};

var authentication = "";
var transport;
var inprogress;

function loadGenres(config, page) {
    if (config != null && config.GenreMappings) {
        if (config.GenreMappings['GENREMOVIE'] != null) {
            page.querySelector('#txtMovieGenre').value = config.GenreMappings['GENREMOVIE'].join(',');
        }
        if (config.GenreMappings['GENRESPORT'] != null) {
            page.querySelector('#txtSportsGenre').value = config.GenreMappings['GENRESPORT'].join(',');
        }
        if (config.GenreMappings['GENRENEWS'] != null) {
            page.querySelector('#txtNewsGenre').value = config.GenreMappings['GENRENEWS'].join(',');
        }
        if (config.GenreMappings['GENREKIDS'] != null) {
            page.querySelector('#txtKidsGenre').value = config.GenreMappings['GENREKIDS'].join(',');
        }
        if (config.GenreMappings['GENRELIVE'] != null) {
            page.querySelector('#txtLiveGenre').value = config.GenreMappings['GENRELIVE'].join(',');
        }
    }
}
export default function(view) {
    view.addEventListener('viewshow', function() {
        Dashboard.showLoadingMsg();
        const page = this;

        ApiClient.getPluginConfiguration(NextPvrConfigurationPage.pluginUniqueId).then(function(config) {
            page.querySelector('#txtWebServiceUrl').value = config.WebServiceUrl || '';
            page.querySelector('#txtPin').value = config.Pin || '';
            page.querySelector('#numPoll').value = config.PollInterval;
            page.querySelector('#chkDebugLogging').checked = config.EnableDebugLogging;
            page.querySelector('#chkInProgress').checked = config.EnableInProgress;
            page.querySelector('#chkNewEpisodes').checked = config.NewEpisodes;
            page.querySelector('#selRecDefault').value = config.RecordingDefault;
            page.querySelector('#selRecTransport').value = config.RecordingTransport;
            loadGenres(config, page);
            authentication = config.WebServiceUrl + config.Pin;
            transport = config.RecordingTransport;
            inprogress = config.EnableInProgress;
            Dashboard.hideLoadingMsg();
        });
    });
    view.querySelector('.nextpvrConfigurationForm').addEventListener('submit', function(e) {
        Dashboard.showLoadingMsg();
        const form = this;

        ApiClient.getPluginConfiguration(NextPvrConfigurationPage.pluginUniqueId).then(function(config) {
            config.WebServiceUrl = form.querySelector('#txtWebServiceUrl').value;
            config.Pin = form.querySelector('#txtPin').value;
            config.EnableDebugLogging = form.querySelector('#chkDebugLogging').checked;
            config.EnableInProgress = form.querySelector('#chkInProgress').checked;
            config.NewEpisodes = form.querySelector('#chkNewEpisodes').checked;
            config.RecordingDefault = form.querySelector('#selRecDefault').value;
            config.RecordingTransport = form.querySelector('#selRecTransport').value;
            config.PollInterval = form.querySelector('#numPoll').value;
            if (authentication != config.WebServiceUrl + config.Pin) {
                config.StoredSid = "";
                config.CurrentWebServiceURL = "";
                // Date will be  updated;
                var myJsDate = new Date();
                config.RecordingModificationTime = myJsDate.toISOString();
            } else if (transport != config.RecordingTransport || inprogress != config.EnableInProgress) {
                var myJsDate = new Date();
                config.RecordingModificationTime = myJsDate.toISOString();
            }
            authentication = config.WebServiceUrl + config.Pin;
            transport = config.RecordingTransport;
            inprogress = config.EnableInProgress;
            // Copy over the genre mapping fields
            config.GenreMappings = {
                'GENREMOVIE': form.querySelector('#txtMovieGenre').value.split(','),
                'GENRESPORT': form.querySelector('#txtSportsGenre').value.split(','),
                'GENRENEWS': form.querySelector('#txtNewsGenre').value.split(','),
                'GENREKIDS': form.querySelector('#txtKidsGenre').value.split(','),
                'GENRELIVE': form.querySelector('#txtLiveGenre').value.split(',')
            };

            ApiClient.updatePluginConfiguration(NextPvrConfigurationPage.pluginUniqueId, config).then(Dashboard.processPluginConfigurationUpdateResult);
        });
        e.preventDefault();
        // Disable default form submission
        return false;
    });
}