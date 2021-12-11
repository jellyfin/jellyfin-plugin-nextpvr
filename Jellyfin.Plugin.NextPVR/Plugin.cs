using System;
using System.Collections.Generic;
using Jellyfin.Plugin.NextPVR.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.NextPVR;

/// <summary>
/// Class Plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly Guid _id = new Guid("9574ac10-bf23-49bc-949f-924f23cfa48f");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override Guid Id => _id;

    /// <inheritdoc />
    public override string Name => "NextPVR";

    /// <inheritdoc />
    public override string Description => "Provides live TV using NextPVR as the backend.";

    /// <summary>
    /// Gets the instance.
    /// </summary>
    /// <value>The instance.</value>
    public static Plugin Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "nextpvr",
                EmbeddedResourcePath = GetType().Namespace + ".Web.nextpvr.html",
            },
            new PluginPageInfo
            {
                Name = "nextpvrjs",
                EmbeddedResourcePath = GetType().Namespace + ".Web.nextpvr.js"
            }
        };
    }
}
