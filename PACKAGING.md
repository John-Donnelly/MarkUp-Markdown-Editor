# MarkUp — Store Packaging & Upload Guide

This guide describes the steps to produce a Microsoft Store–ready MSIX package for **MarkUp Markdown Editor**.

---

## Prerequisites

| Requirement | Notes |
|---|---|
| Visual Studio 2022+ with Windows App SDK workload | Or VS 2026 Insiders |
| Windows App SDK 1.8 | Installed via NuGet (automatic on build) |
| `MakePri.exe` / `makeappx.exe` | Included with the Windows SDK |
| Store Publisher certificate | Stored in `MarkUp Markdown Editor/Assets/` or via Partner Center |
| Microsoft Partner Center account | Required for Store submission |

> **IL Trimming is permanently disabled** (`PublishTrimmed=False` in the .csproj). Do not
> re-enable it — WinUI 3 / Windows App SDK are not trim-compatible and trimmed packages
> fail at runtime with `MissingMethodException`.

---

## Building the Store Package

### Via Visual Studio (recommended)

1. Set the **Solution Configuration** to **Release** and the **Solution Platform** to
   **x64** (repeat for **x86** and **ARM64** if producing a multi-arch bundle).
2. Right-click the `MarkUp Markdown Editor` project → **Publish** →
   **Create App Packages…**
3. Choose **Microsoft Store** → select or create your Partner Center app association.
4. Set the **Version** to match `<Version>` in the `.csproj` (e.g. `1.8.0.0`).
   The version in `Package.appxmanifest` must also match — both are updated automatically
   by the release process.
5. Select all three architectures (x86, x64, ARM64).
6. Click **Create** — VS produces an `.msixbundle` and per-arch `.appxsym` files under
   `AppPackages\StoreUpload\`.

### Via Command Line

```powershell
# From the repo root
dotnet build "MarkUp Markdown Editor\MarkUp Markdown Editor.csproj" `
	-c Release -p:Platform=x64 `
	-p:GenerateAppxPackageOnBuild=true `
	-p:UapAppxPackageBuildMode=StoreUpload `
	-p:AppxBundle=Always `
	-p:AppxBundlePlatforms="x86|x64|ARM64" `
	"-p:PdbCmfx64ExeFullPath=$VC\x64\mspdbcmf.exe" `
	"-p:PdbCmfx86ExeFullPath=$VC\x86\mspdbcmf.exe"
# where $VC points at the MSVC host tools, e.g.
# $VC = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC\14.44.35207\bin\Hostx64"
```

The two `PdbCmf*` properties are only needed outside a Visual Studio developer shell: the
symbol-package targets look for `mspdbcmf.exe` via `VCToolsInstallDir`, which the plain
`dotnet` CLI does not set, and without it the build fails with MSB4044 / MSB6011 after the
bundle is written (the `.msixupload` would then lack the `.appxsym` files).

`-p:Platform=x64` is required (without it the packaging targets fail with "cannot be
ProcessorArchitecture neutral"); the bundle still contains all three architectures.
`UapAppxPackageBuildMode=StoreUpload` produces the Partner Center artefact
`AppPackages\MarkUp Markdown Editor_<version>_x86_x64_ARM64_bundle.msixupload` (bundle + symbols in
one file). Without it you get a dev-signed sideload bundle under
`AppPackages\MarkUp Markdown Editor_<version>_Test\`, which the Store will not accept.

---

## Uploading to Partner Center

1. Sign in to [Partner Center](https://partner.microsoft.com/dashboard).
2. Select **MarkUp Markdown Editor** → **Start a new submission** (or update an existing draft).
3. In **Packages**, upload `MarkUp Markdown Editor_<version>_x86_x64_ARM64_bundle.msixupload`
   (it already contains the bundle and the three per-arch `.appxsym` symbol files).
4. Complete the **Store listing** and **Pricing/Availability** sections (no changes needed for
   a patch/minor release — the existing text carries over).
5. Click **Submit to the Store**.

---

## Release Checklist

- [ ] All unit tests pass (`dotnet test MarkUp.Tests/MarkUp.Tests.csproj` → 487/487).
- [ ] `<Version>` in `MarkUp Markdown Editor.csproj` updated.
- [ ] `Package.appxmanifest` `Version` attribute updated (format `X.Y.Z.0`).
- [ ] `CHANGELOG.md` entry added with the correct date (`git log -1 --format=%ad --date=short`).
- [ ] Production `jad-apps-site-git`: `data/news.json` item added, `data/sync-state.json` advanced, `data/studio-projects.ts` refreshed. Do not update the stale `jad-apps-site` Pages project.
- [ ] `PublishTrimmed` is `False` — verify before every Store build.
- [ ] Store package built and smoke-tested locally (sideload or clean VM).
- [ ] Partner Center upload uses the generated `*_bundle.msixupload` artefact; it already contains the `.msixbundle` and `.appxsym` files.
- [ ] Submission certified and published.
- [ ] Git tag created: `git tag v<version> && git push origin v<version>`.
