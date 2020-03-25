using System;
using System.IO;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Serialization;
using NextPvr.Helpers;

namespace NextPvr.Responses
{
    public class InitializeResponse
    {
        public bool LoggedIn(Stream stream, IJsonSerializer json, ILogger logger)
        {
            var root = json.DeserializeFromStream<RootObject>(stream);

            if (root.SIDValidation != null)
            {
                UtilsHelper.DebugInformation(logger,string.Format("[NextPvr] Connection validation: {0}", json.SerializeToString(root)));
                return root.SIDValidation.validated;
            }

            logger.LogError("[NextPvr] Failed to validate your connection with NextPvr.");
            throw new Exception("Failed to validate your connection with NextPvr.");
        }

        public class SIDValidation
        {
            public bool validated { get; set; }
        }

        public class RootObject
        {
            public SIDValidation SIDValidation { get; set; }
        }
    }
}
