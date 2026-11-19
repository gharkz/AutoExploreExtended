# Building

Requires the .NET SDK, the .NET Framework 4.8 targeting pack, Elin and AutoExplore installed.
Set `ElinDir` to the Elin installation and `AutoExploreDll` to the installed `Elin_AutoExplore.dll`
(both default to the author's paths), then run:

```text
dotnet build AutoExploreExtended.csproj -c Release
```

Output: `bin/x64/Release/net48/AutoExploreExtended.dll`. No game or dependency assemblies are
redistributed. Copy the DLL, `package.xml` and `preview.jpg` into an Elin Package subfolder to install.

The package ID is `gharkz.autoexploreextended`. Version 1.0.0 used the development ID
`gharkz.autoexplorelockpicking`; 1.0.1 copies those settings files to the new names once. The preview image is drawn by `tools/make_preview.py` (Python + Pillow).
