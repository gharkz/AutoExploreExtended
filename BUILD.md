# Building

Requires the .NET SDK, the .NET Framework 4.8 targeting pack, Elin and AutoExplore installed.
Set `ElinDir` to the Elin installation and `AutoExploreDll` to the installed `Elin_AutoExplore.dll`
(both default to the author's paths), then run:

```text
dotnet build AutoExploreLockpicking.csproj -c Release
```

Output: `bin/x64/Release/net48/AutoExploreLockpicking.dll`. No game or dependency assemblies are
redistributed. Copy the DLL, `package.xml` and `preview.jpg` into an Elin Package subfolder to install.

The package ID is `gharkz.autoexplorelockpicking` (kept from development; the display name is
AutoExplore Extended). The preview image is drawn by `tools/make_preview.py` (Python + Pillow).
