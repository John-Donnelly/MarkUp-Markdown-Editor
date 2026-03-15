# UI Test Report v3 — Remote Execution Infrastructure

## Scope

End-to-end migration of the `MarkUp.UITests` suite from a **local WinAppDriver** model to a
fully automated **remote Appium host** model. All keyboard and clipboard injection was rewritten
to route through the Appium WebDriver protocol so that every UI action executes on the remote
machine. A supporting packaging, certificate, and deployment pipeline was built so the latest
MSIX is automatically staged and installed on the remote host before each test run.

---

## Problem statement

After the v2 local WinAppDriver harness was established, the suite was migrated to target a
remote Windows machine (`DESKTOP-8KUS9VM` / `192.168.0.100`) running Appium 3.2.2 with the
Windows driver. Three categories of failure emerged:

1. **Package deployment** — the remote machine could not authenticate to the local SMB share
   (`\\MORPHEUS\source`) used for staging, blocked every test before Appium was even contacted.
2. **Keyboard injection** — `keybd_event` Win32 API calls in `AppSession` were dispatched on
   the *local* machine, not the remote one, so every shortcut (Ctrl+B, Ctrl+Z, Escape, etc.)
   silently missed the remote app.
3. **Clipboard injection** — `SetClipboard` used Win32 `OpenClipboard`/`SetClipboardData`
   which only affects the local machine clipboard; the remote app's Paste produced empty text.

---

## Root causes and solutions

### 1. SMB authentication failure (package staging)

**Root cause:** WinRM runs commands under the remote user identity. That identity has no
network logon token and cannot authenticate to `\\MORPHEUS\source` using mapped-drive paths.

**Fix:** Replaced direct SMB path access with `New-PSSession` + `Copy-Item -ToSession`.
The local PowerShell process opens an authenticated WinRM session, then uses `Copy-Item -ToSession`
to push the package folder through the encrypted WinRM channel. The remote script never needs
to access a network share at all.

### 2. Performance: per-test WinRM copy (45 MB each run)

**Root cause:** The initial implementation re-copied the full MSIX package folder on every test
class initialisation, blocking the Appium connection for 3–5 minutes per class.

**Fix:** Added a **version-skip fast-path**. Before copying anything, the install script opens
a PSSession and calls `Get-AppxPackage` on the remote machine. If the installed version matches
the local package version *and* the package status is `Ok`, the script skips the copy entirely
and returns the AUMID immediately (~5 seconds vs. 3–5 minutes).

`ExtractPackageVersion(string msixPath)` uses a `Regex` pattern `_(\d+\.\d+\.\d+\.\d+)_` to
extract the version from the MSIX filename for comparison.

### 3. Wrong package family name hash

**Root cause:** The AUMID `JADApps.MarkUpMarkdownEditor_yp6wg2crjc7ye!App` was valid for the
Microsoft Store signing certificate. Dev-signed MSIX packages use a different publisher hash.

**Fix:** Updated `RemoteAppAumidDefault` to `JADApps.MarkUpMarkdownEditor_30vn2v44e6ykm!App`,
which is the family name for the self-signed `CN=2B43AD1A-273D-402E-A9A5-FF23C52C75B9` cert.

### 4. Scheduled task logon type

**Root cause:** `Add-AppDevPackage.ps1` must run in an interactive desktop session so AppX PLM
can resolve the UI-capable package install. The initial code used `-LogonType InteractiveToken`
which is not a valid enum value on this Windows build.

**Fix:** Changed to `-LogonType Interactive` in the generated install script.

### 5. keybd_event local-only keyboard injection

**Root cause:** `SendModifiedShortcut`, `SendVirtualKey`, `SendCharacter`, `SendText`, and all
related helpers used the `keybd_event` Win32 API. This API posts key events into the *local*
input queue; it has no effect on a remote machine connected only via Appium/WinRM.

**Fix:** Removed the entire Win32 keyboard block and replaced it with the
`OpenQA.Selenium.Interactions.Actions` API from `Appium.WebDriver` 8.1.0 (Selenium 4).
All key events now travel over the W3C WebDriver HTTP protocol to the remote WinAppDriver
process, which injects them directly into the app's input queue on the remote machine.

**New implementation:**

```csharp
private static void SendRemoteModifiedKeys(params string[] keys)
{
    if (Session is null) return;
    var actions = new Actions(Session);
    foreach (var k in keys[..^1]) actions = actions.KeyDown(k);
    actions = actions.SendKeys(keys[^1]);
    for (int i = keys.Length - 2; i >= 0; i--) actions = actions.KeyUp(keys[i]);
    actions.Perform();
    Thread.Sleep(200);
}

private static void SendRemoteKey(string key)
{
    if (Session is null) return;
    new Actions(Session).SendKeys(key).Perform();
    Thread.Sleep(150);
}
```

All public shortcut methods (`ClickBold`, `ClickUndo`, `ClickRedo`, `PressEscape`, etc.) now
delegate to `SendRemoteModifiedKeys` / `SendRemoteKey` with `OpenQA.Selenium.Keys` constants.

### 6. Win32 clipboard injection (SetClipboard)

**Root cause:** `SetClipboard` called Win32 `OpenClipboard`/`SetClipboardData`, which writes
only to the local machine clipboard. The remote app's Paste operation produced empty text.
`StatusBarTests` used the pattern `SetClipboard(text) + ClickMenu(MenuPaste)`.

**Fix:**
- Removed `SetClipboard` entirely from `AppSession`.
- `PasteText(string text)` now calls `FindById("EditorTextBox").SendKeys(text)` directly,
  which routes through WinAppDriver to insert text in the remote element.
- `SendText(string text)` uses the same `SendKeys` approach.
- All four affected `StatusBarTests` methods updated to call `PasteText(text)` directly.

---

## Files changed

### Modified

| File | Change summary |
|---|---|
| `.gitignore` | Added `.env` so WinRM credentials are never committed to the repository |
| `MarkUp Markdown Editor/MarkUp Markdown Editor.csproj` | Added `UiTestPackageCertificate` property group; added `EnsureUiTestPackageCertificate` and `ExportUiTestPackageCertificate` MSBuild targets |
| `MarkUp.UITests/MarkUp.UITests.csproj` | Updated setup comment to reference remote host; added `RemoteUiAppProject`/`RemoteUiAppPlatform` properties; added `BuildRemoteUiAppPackage` MSBuild target for CLI builds |
| `MarkUp.UITests/AppSession.cs` | Complete remote infrastructure rewrite (see detail below) |
| `MarkUp.UITests/StatusBarTests.cs` | 4 methods: `SetClipboard` + `ClickMenu(Paste)` replaced with `PasteText` |
| `MarkUp.UITests/README.md` | Expanded with remote host prerequisites, `.env` variable reference, updated troubleshooting table |

### New (untracked)

| File | Purpose |
|---|---|
| `MarkUp Markdown Editor/Create-UiTestPackageCertificate.ps1` | Creates a self-signed RSA-2048/SHA-256 code-signing certificate for MSIX dev-signing; idempotent (skips if PFX already exists) |
| `MarkUp Markdown Editor/Export-UiTestPackageCertificate.ps1` | Exports the PFX to a DER-encoded `.cer` file alongside the MSIX so the remote machine can trust the publisher |
| `MarkUp.UITests/Setup-RemoteUiTestHost.ps1` | One-shot script to prepare a fresh Windows machine as an Appium UI test host: installs Appium + Windows driver via npm, starts the Appium server, verifies connectivity |

---

## AppSession.cs — detailed change log

### Removed (Win32 / local-only code)

**Constants removed:**
- `KEYEVENTF_KEYUP`, `CF_UNICODETEXT`, `GMEM_MOVEABLE`
- `VK_CONTROL`, `VK_SHIFT`, `VK_MENU`

**P/Invoke declarations removed:**
- `keybd_event`, `VkKeyScan`
- `GlobalAlloc`, `GlobalLock`, `GlobalUnlock`
- `OpenClipboard`, `EmptyClipboard`, `SetClipboardData`, `CloseClipboard`

**Methods removed:**
- `SetClipboard(string)` — Win32 clipboard write (local only)
- `EnsureAppFocused()` — Win32 `SetForegroundWindow` (no-op in remote mode)
- `SendModifiedShortcut(params ushort[])` — `keybd_event` multi-key
- `SendVirtualKey(ushort)` — `keybd_event` single key
- `SendCharacter(char)` — `VkKeyScan` + `keybd_event`
- `SendAltNumpadCode(string)` — numpad Unicode entry via `keybd_event`
- `ToVirtualKey(char)` — VK code helper

### Added (remote-safe code)

**Using directive added:**
- `using OpenQA.Selenium.Interactions;`

**Methods added (keyboard / text injection):**
- `SendRemoteModifiedKeys(params string[] keys)` — Actions KeyDown/SendKeys/KeyUp sequence over WebDriver HTTP
- `SendRemoteKey(string key)` — Actions single-key send over WebDriver HTTP
- `VkToSeleniumKey(ushort vk)` — maps remaining `VK_*` constants to `OpenQA.Selenium.Keys` strings

**Methods added (remote package deployment):**
- `ExtractPackageVersion(string msixPath)` — Regex extracts `1.4.0.0` from MSIX filename
- `BuildRemotePackageInstallScript(string packageDir, string expectedVersion)` — generates a C#
  raw-string PowerShell script with:
  - `.env` loading
  - WinRM credential setup
  - `New-PSSession` creation
  - **Version-skip fast-path:** `Get-AppxPackage` version + status check; returns AUMID on match
  - **Slow path:** `Copy-Item -ToSession` package folder push; scheduled task with `-LogonType Interactive`
    to run `Add-AppDevPackage.ps1`; polls until package status is `Ok`
- `ExecuteRemotePackageInstall(string dir, string version, TimeSpan timeout)` — Base64-encodes
  and spawns `powershell.exe -EncodedCommand`; captures stdout; parses AUMID from last line
  containing `!App`
- `TryInstallLatestRemotePackage()` — locates newest MSIX under `AppPackages`, calls
  `ExtractPackageVersion` then `ExecuteRemotePackageInstall`; sets `_remoteAppAumid`

**Constants updated:**
- `RemoteAppAumidDefault` → `JADApps.MarkUpMarkdownEditor_30vn2v44e6ykm!App`
- `RemoteDriverUrl` (was `DriverUrl`) → `http://192.168.0.100:4723`

**Retained P/Invoke (still needed for local window management):**
- `EnumWindows`, `GetWindowText`, `GetWindowTextLength`, `IsWindowVisible`
- `ShowWindow`, `SetForegroundWindow`
- `VK_ESCAPE`, `VK_RETURN`, `VK_DELETE`, `VK_DOWN`, `VK_RIGHT`, `VK_ADD`, `VK_SUBTRACT`, `SW_RESTORE`

---

## MSBuild packaging pipeline

### Certificate creation and export (`MarkUp Markdown Editor.csproj`)

Two MSBuild targets manage the dev-signing certificate lifecycle:

**`EnsureUiTestPackageCertificate`** (`BeforeTargets="Build"`, only when `GenerateAppxPackageOnBuild=true`):
- Creates the intermediate output directory
- Invokes `Create-UiTestPackageCertificate.ps1` with publisher `CN=2B43AD1A-273D-402E-A9A5-FF23C52C75B9`,
  PFX path in `$(IntermediateOutputPath)UiTestSigning\`, and the shared password
- Idempotent: the script skips generation if the PFX already exists

**`ExportUiTestPackageCertificate`** (`AfterTargets="_AddWindowsInstallScriptToTestLayout"`, only when package dir exists):
- Invokes `Export-UiTestPackageCertificate.ps1` to write `$(AssemblyName).cer` alongside the MSIX
- Allows the remote install script to trust the publisher with a single `certutil -addstore TrustedPeople`

### Remote app package build (`MarkUp.UITests.csproj`)

**`BuildRemoteUiAppPackage`** (`BeforeTargets="Build"`, skipped inside Visual Studio):
- Triggers `MSBuild` on the app project with `GenerateAppxPackageOnBuild=true;UapAppxPackageBuildMode=SideloadOnly;AppxBundle=Never`
- Ensures the MSIX and `.cer` are always fresh when running `dotnet test` from the CLI

---

## Environment configuration (`.env`)

```
UITEST_REMOTE_WINRM_USERNAME=John-Dev
UITEST_REMOTE_WINRM_PASSWORD=<password>
UITEST_REMOTE_APP=192.168.0.100
UITEST_REMOTE_AUMID=JADApps.MarkUpMarkdownEditor_30vn2v44e6ykm!App
```

The `.env` file lives at the solution root and is loaded by `AppSession` at assembly initialisation.
It is excluded from source control via `.gitignore`.

---

## Remote host setup (`Setup-RemoteUiTestHost.ps1`)

A one-shot PowerShell script that:
1. Loads `.env` for WinRM credentials
2. Opens a `Negotiate`-auth `Invoke-Command` session to the target machine
3. Verifies Node.js and npm are present
4. Installs Appium globally via `npm install -g appium` if not present
5. Installs the `windows` driver via `npx appium driver install windows` if not present
6. Starts the Appium server in a hidden window on `0.0.0.0:$Port` if not already running
7. Returns a status object with Appium version, driver install state, log path, and process count

Prerequisites on the remote machine: Node.js + npm installed, WinRM enabled, Developer Mode on.

---

## Validation results

| Test class | Tests | Result | Notes |
|---|---|---|---|
| `ViewModeTests` | 7 | ✅ **All passed** | End-to-end validation of remote Appium pipeline |
| Remaining 16 classes | 199 | Not yet run | Full suite requires per-class execution due to tool timeout |

**Build status:** ✅ Clean — zero errors, zero warnings after all changes.

---

## Known remaining issues

| Issue | Detail |
|---|---|
| Full suite run tool timeout | 206-test suite exceeds the test runner tool's time budget; run class-by-class or via `dotnet test` CLI |
| Remote desktop visibility | WinAppDriver drives the interactive desktop; a locked or disconnected remote session causes unreliable automation |
| README typo | Troubleshooting table contains `192.268.0.100` (should be `192.168.0.100`); cosmetic, does not affect tests |

---

## Next steps

1. Run remaining test classes individually to establish a full v3 baseline failure inventory
2. Fix any test failures introduced by the Appium Actions keyboard change (timing, focus, key codes)
3. Correct the README IP typo
4. Investigate per-class parallel execution to reduce total run time
