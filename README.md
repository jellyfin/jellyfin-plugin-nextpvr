<h1 align="center">Jellyfin NextPVR Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.org">Jellyfin Project</a></h3>

<p align="center">
<img alt="Logo Banner" src="https://raw.githubusercontent.com/jellyfin/jellyfin-ux/master/branding/SVG/banner-logo-solid.svg?sanitize=true"/>
<br/>
<br/>
<a href="https://github.com/jellyfin/jellyfin-plugin-nextpvr/actions?query=workflow%3A%22Test+Build+Plugin%22">
<img alt="GitHub Workflow Status" src="https://img.shields.io/github/workflow/status/jellyfin/jellyfin-plugin-nextpvr/Test%20Build%20Plugin.svg">
</a>
<a href="https://github.com/jellyfin/jellyfin-plugin-nextpvr">
<img alt="MIT License" src="https://img.shields.io/github/license/jellyfin/jellyfin-plugin-nextpvr.svg"/>
</a>
<a href="https://github.com/jellyfin/jellyfin-plugin-nextpvr/releases">
<img alt="Current Release" src="https://img.shields.io/github/release/jellyfin/jellyfin-plugin-nextpvr.svg"/>
</a>
</p>

## About

The Jellyfin NextPVR plugin can be used for watching media provided by a <a href="http://www.nextpvr.com">NextPVR</a> server.

## Build & Installation Process

1. Clone this repository

2. Ensure you have .NET Core SDK set up and installed

3. Build the plugin with your favorite IDE or the `dotnet` command

```sh
dotnet publish --configuration Release --output bin
```

4. Place the resulting `Jellyfin.Plugin.NextPvr.dll` file in a folder called `plugins/` inside your Jellyfin data directory
