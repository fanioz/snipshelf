# MSIX CI fallback recipe (GitHub Actions, windows-latest)

Status: **written, not yet executed on a runner** — verify on the first run (part of the packaging backlog issue).

This is the fallback the PRD (§8 M0, §12) demands in case Avalonia Parcel hiccups. It is also the **free, automatable** packaging path: empirical probing found Parcel's CLI is license-gated (see below), while this recipe uses only standard tooling preinstalled on GitHub's `windows-latest` runners.

## Why this exists — Parcel licensing findings (2026-10-03)

- Parcel **1.1.1** installs fine on macOS (`dotnet tool install --global AvaloniaUI.Parcel`) and its docs state MSIX is built "on every supported host" **without the Windows SDK** (`parcel pack App.parcel -r win-x64 -p msix -o ./artifacts`).
- But every CLI invocation is hard-gated: `AvaloniaLicensingException: pass --license-key, set AVALONIA_TOOLS_LICENSE_KEY, or have an unexpired session in Parcel GUI app`. The **community (free) license covers the GUI only**; CLI automation requires a paid tier (Avalonia Plus/pro) **or** an unexpired GUI session (needs an Avalonia Portal sign-in; the CLI-piggyback-on-GUI-session path is unverified).
- The `.parcel` project file is hand-authored JSON (property names from the configuration reference, e.g. `GeneralSettings.NetProjectPath`, `Win32Settings.SigningType: "None"` for Store submission — the Store signs the uploaded package).
- Verified locally on macOS regardless of Parcel: `dotnet publish -c Release -r win-x64 --self-contained true` cross-compiles the Avalonia 11.3.22 app to a Windows exe without any Windows tooling (probe: `prototype/prove-parcel` branch).

## Workflow (`.github/workflows/msix.yml`)

```yaml
name: MSIX
on:
  workflow_dispatch: {}

jobs:
  msix:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Publish (win-x64, self-contained)
        run: dotnet publish src/SnipShelf/SnipShelf.csproj -c Release -r win-x64 --self-contained true -o publish

      - name: Stage MSIX payload
        shell: pwsh
        run: |
          New-Item -ItemType Directory -Force -Path msix-payload | Out-Null
          Copy-Item -Recurse -Force publish/* msix-payload/
          Copy-Item packaging/AppxManifest.template.xml msix-payload/AppxManifest.xml
          Copy-Item packaging/[Content_Types].xml msix-payload/[Content_Types].xml

      - name: MakeAppX
        shell: pwsh
        run: |
          $makeappx = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter makeappx.exe |
            Where-Object { $_.FullName -match 'x64' } |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
          & $makeappx pack /d msix-payload /p SnipShelf.msix /nv

      - uses: actions/upload-artifact@v4
        with:
          name: SnipShelf-msix
          path: SnipShelf.msix
```

## Payload templates (repo `packaging/` dir)

`[Content_Types].xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="dll" ContentType="application/octet-stream" />
  <Default Extension="exe" ContentType="application/octet-stream" />
  <Default Extension="png" ContentType="image/png" />
  <Default Extension="xml" ContentType="application/vnd.ms-appx.manifest" />
</Types>
```

`AppxManifest.template.xml` — **Identity Name and Publisher must be replaced with the values Partner Center assigns after app reservation** (ties to wayfinder ticket "Partner Center account + name reservation"):

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package
    xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
    xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10">
  <Identity Name="PARTNER-CENTER-IDENTITY" Publisher="CN=PARTNER-CENTER-PUBLISHER" Version="1.0.0.0" />
  <Properties>
    <DisplayName>SnipShelf</DisplayName>
    <PublisherDisplayName>fanioz</PublisherDisplayName>
    <Logo>assets\icon.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Applications>
    <Application Id="App">
      <uap:VisualElements DisplayName="SnipShelf" Description="Local-first snippet &amp; AI-prompt vault"
                          BackgroundColor="transparent" Square150x150Logo="assets\icon.png"
                          Square44x44Logo="assets\icon.png" />
    </Application>
  </Applications>
</Package>
```

Notes:
- `MinVersion="10.0.17763.0"` = Windows 10 1809, per PRD §"Platform".
- `/nv` (no validation) keeps MakeAppX from failing on unsigned-package expectations; the Store ingestion check is the real validation.
- No Signtool step: the **Store signs the submitted package** (PRD §12). Add signing only if a direct-distribution channel is ever opened.
- `assets\icon.png` entries are placeholders until the icon decision lands (wayfinder fog → ticket "Decide the app icon source"); Parcel-derived MSIX visual assets come from a multi-resolution `.ico` via csproj `<ApplicationIcon>` — the CI path needs equivalent PNGs.
