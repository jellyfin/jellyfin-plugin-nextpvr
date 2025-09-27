# Jellyfin NextPVR Plugin - Build Fixed Version

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/1000mani/jellyfin-plugin-nextpvr)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Jellyfin Version](https://img.shields.io/badge/Jellyfin-10.10.7-purple)](https://jellyfin.org/)

This is a fork of the official Jellyfin NextPVR plugin with build fixes and improvements for .NET 8.0 and Jellyfin 10.10.7 compatibility.

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK or later
- Windows 10/11 (for the batch script)
- Jellyfin Server 10.10.7 or later

### Easy Build (Recommended)
Simply run the provided build script:
```bash
build_nextpvr.bat
```

This script will:
- Clean previous builds
- Restore NuGet packages
- Build the plugin
- Publish to the `bin` directory

### Manual Build
If you prefer to build manually:

```bash
cd Jellyfin.Plugin.NextPVR
dotnet clean
dotnet restore
dotnet publish --configuration Release --output bin
```

## 🔧 What's Fixed

This fork addresses several build issues found in the original repository:

### 1. Package Version Updates
- **Jellyfin.Controller**: Updated to version 10.8.13
- **Jellyfin.Extensions**: Updated to version 10.8.13
- **Target Framework**: Confirmed .NET 8.0 compatibility

### 2. NuGet Configuration
- Added `NuGet.config` with proper Jellyfin package source
- Ensures correct package resolution during restore

### 3. Build Script
- Created `build_nextpvr.bat` for one-command building
- Handles clean, restore, and publish automatically
- Perfect for developers and CI/CD pipelines

## 📁 Project Structure

```
jellyfin-plugin-nextpvr/
├── Jellyfin.Plugin.NextPVR/          # Main plugin project
│   ├── bin/                          # Build output directory
│   ├── Configuration/                # Plugin configuration
│   ├── Entities/                     # Data models
│   ├── Helpers/                      # Utility classes
│   ├── Responses/                    # API response models
│   ├── Web/                          # Web interface files
│   └── Jellyfin.Plugin.NextPVR.csproj
├── build_nextpvr.bat                 # Build script
├── NuGet.config                      # NuGet package sources
└── README.md                         # This file
```

## 🛠️ Development

### Building from Source
1. Clone this repository:
   ```bash
   git clone https://github.com/1000mani/jellyfin-plugin-nextpvr.git
   cd jellyfin-plugin-nextpvr
   ```

2. Run the build script:
   ```bash
   build_nextpvr.bat
   ```

3. The built plugin will be in `Jellyfin.Plugin.NextPVR/bin/`

### Installing the Plugin
1. Copy `Jellyfin.Plugin.NextPVR.dll` to your Jellyfin plugins directory
2. Restart Jellyfin server
3. Configure the plugin in the Jellyfin web interface

## 🐛 Issues Fixed

- **Build Failures**: Resolved package version conflicts
- **Missing Dependencies**: Added proper NuGet configuration
- **Complex Build Process**: Simplified with automated build script
- **.NET 8.0 Compatibility**: Verified and tested

## 🤝 Contributing

This fork is based on the official Jellyfin NextPVR plugin. If you find additional issues or improvements:

1. Fork this repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## 📋 Original Repository

This is a fork of the official Jellyfin NextPVR plugin:
- **Original**: https://github.com/jellyfin/jellyfin-plugin-nextpvr
- **Purpose**: Build fixes and .NET 8.0 compatibility improvements

## 📄 License

This project maintains the same license as the original Jellyfin NextPVR plugin.

## 🙏 Acknowledgments

- Jellyfin team for the original plugin
- .NET community for excellent tooling
- Contributors who helped identify build issues

---

**Need Help?** Open an issue or check the [Jellyfin Community Forums](https://forum.jellyfin.org/).

**Build Issues?** This fork specifically addresses common build problems. Try the `build_nextpvr.bat` script first!