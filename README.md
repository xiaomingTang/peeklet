# Peeklet

Native Windows file quick preview prototype built with WPF on .NET 8.

## Current status

- Tray-resident background app and global space trigger are implemented.
- Left and right arrows navigate within the current Explorer file list while preview is open.
- Placement strategy prefers right-top, then right-center, right-bottom, left-top, left-center, left-bottom, center-top, center-center, center-bottom.
- Explorer foreground window detection and selected file lookup are implemented with Shell COM plus UI Automation bounds lookup.
- Preview routing supports images, text, markdown, PDF, SVG, and Windows Preview Handler hosting for Office or any registered shell preview type.
- WebView2 is preloaded on startup to reduce first-open latency for browser-backed previews.

## Planned next steps

- Add tray process, warm startup, and caching.
- Add left/right file navigation and preloading.

## Build

This machine currently has only the .NET runtime installed. To build locally, install the .NET 8 SDK and then run:

```powershell
dotnet restore
dotnet build
```

## GitHub Actions

- Pushing a tag that matches `v*` triggers the workflow in `.github/workflows/tag-build.yml`.
- The workflow runs on `windows-latest`, restores dependencies, builds in `Release`, publishes with the `Properties/PublishProfiles/GitHubRelease.pubxml` profile, zips the published output, uploads both the folder and zip as Actions artifacts, and creates a GitHub Release for the tag.
- Example tags: `v0.1.0`, `v1.0.0`.

## Distribution Publish Profile

The default end-user distribution profile is `Properties/PublishProfiles/GitHubRelease.pubxml`.

- `RuntimeIdentifier=win-x64`: targets the most common Windows desktop architecture.
- `SelfContained=true`: users do not need to install the .NET runtime separately.
- `UseAppHost=true`: produces a normal Windows `.exe` launcher.
- `PublishSingleFile=false`: keeps the app as a folder-based distribution for better compatibility with WPF, WebView2, and shell preview handler integration.
- `PublishTrimmed=false`: avoids trim-related breakage in reflection-heavy desktop and COM scenarios.
- `DebugSymbols=false`: keeps the release package smaller.

Local publish command:

```powershell
dotnet publish .\Peeklet.csproj -p:PublishProfile=Properties/PublishProfiles/GitHubRelease.pubxml -o .\artifacts\publish
```