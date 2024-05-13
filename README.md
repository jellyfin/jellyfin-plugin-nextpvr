<h1 align="center">Jellyfin NextPVR Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.org">Jellyfin Project</a></h3>

<p align="center">
<img alt="Plugin Banner" src="https://raw.githubusercontent.com/jellyfin/jellyfin-ux/master/plugins/SVG/jellyfin-plugin-nextpvr.svg?sanitize=true"/>
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

This plugin provides access to live TV, program guide, and recordings from a [NextPVR](http://www.nextpvr.com) server.

## Installation

[See the official documentation for install instructions](https://jellyfin.org/docs/general/server/plugins/index.html#installing).

## Build

1. To build this plugin you will need [.Net 8.x](https://dotnet.microsoft.com/download/dotnet/8.0).

2. Build plugin with following command
  ```
  dotnet publish --configuration Release --output bin
  ```

3. Place the dll-file in the `plugins/nextpvr` folder (you might need to create the folders) of your JF install

## Releasing

To release the plugin we recommend [JPRM](https://github.com/oddstr13/jellyfin-plugin-repository-manager) that will build and package the plugin.
For additional context and for how to add the packaged plugin zip to a plugin manifest see the [JPRM documentation](https://github.com/oddstr13/jellyfin-plugin-repository-manager) for more info.

## Contributing

We welcome all contributions and pull requests! If you have a larger feature in mind please open an issue so we can discuss the implementation before you start.
In general refer to our [contributing guidelines](https://github.com/jellyfin/.github/blob/master/CONTRIBUTING.md) for further information.

## Licence

This plugins code and packages are distributed under the MIT License. See [LICENSE](./LICENSE) for more information.
