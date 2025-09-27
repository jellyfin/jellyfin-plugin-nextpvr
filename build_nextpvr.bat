@echo off
echo Building NextPVR Plugin...
cd Jellyfin.Plugin.NextPVR
dotnet clean
dotnet restore
dotnet publish --configuration Release --output bin
echo Build complete!
pause
