<h1 align="center">Jellyfin NextPVR Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.org/">Jellyfin Project</a></h3>

## About

The Jellyfin NextPVR plugin can be used for watching media provided by a <a href="http://www.nextpvr.com/">NextPVR</a> server.

## Build & Installation Process

1. Clone this repository
2. Ensure you have .NET Core SDK set up and installed
3. Build the plugin with your favorite IDE or the `dotnet` command:

```
dotnet publish --configuration Release --output bin
```

4. Place the resulting `Jellyfin.Plugin.NextPvr.dll` file in a folder called `plugins/` inside your Jellyfin data directory

### Settings Screenshot

<img src=screenshot.png>
