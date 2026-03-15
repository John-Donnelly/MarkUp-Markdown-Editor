# MarkUp.UITests — End-to-End UI Test Suite

Automated UI tests for **MarkUp Markdown Editor** using [WinAppDriver](https://github.com/microsoft/WinAppDriver) (Microsoft's accessibility-based Windows UI Automation driver) and the Appium WebDriver client.

---

## Prerequisites

### 1 — Remote WinAppDriver host

Download and install **WinAppDriver 1.2.1** from:
https://github.com/microsoft/WinAppDriver/releases/tag/v1.2.1

WinAppDriver requires **Windows 10 Developer Mode** to be enabled:
- Settings → Privacy & Security → For Developers → Developer Mode: **On**

The UI test suite is configured to always connect to the remote WinAppDriver service at:

```text
http://192.168.0.100:4723
```

`WinAppDriver.exe` must already be running on that remote Windows machine before the test run starts.
The test project no longer starts a local WinAppDriver instance.

To install Appium on the remote machine, install the Windows driver, and start the Appium server through WinRM, run:

```powershell
.\MarkUp.UITests\Setup-RemoteUiTestHost.ps1
```

The script reads `UITEST_REMOTE_WINRM_USERNAME` and `UITEST_REMOTE_WINRM_PASSWORD` from the root `.env` file, connects to the remote machine with WinRM, runs `npm install -g appium` when needed, runs `npx appium driver install windows`, and starts the server with:

```text
npx appium server --address 0.0.0.0 --port 4723
```

### 2 — Automatic package install on the remote machine

The UI test startup now installs the latest x64 MSIX package before it opens the app session on the remote machine.
The package build generates a local dev-signing certificate that matches the app manifest publisher, and the remote installer stages the full package folder before invoking the generated `Add-AppDevPackage.ps1` script.

Set one of these before running the tests when the remote machine does not match the checked-in defaults:

- `UITEST_REMOTE_APP` - full WinAppDriver `app` capability value; can be an AUMID or a full executable path
- `UITEST_REMOTE_AUMID` - packaged app AUMID on the remote machine
- `UITEST_REMOTE_APP_PATH` - full executable path on the remote machine
- `UITEST_REMOTE_SHARE_ROOT` - remote share root that mirrors this workspace; defaults to `Z:\`
- `UITEST_REMOTE_WINRM_USERNAME` - WinRM user for package installation on the remote machine
- `UITEST_REMOTE_WINRM_PASSWORD` - WinRM password for that remote user
- `UITEST_SOURCE_SHARE_USERNAME` - optional username for accessing `\\MORPHEUS\source` during remote package staging
- `UITEST_SOURCE_SHARE_PASSWORD` - optional password for that source-share account

The UI test bootstrap automatically loads these values from the workspace root `.env` file when they are not already present in the environment. When package installation runs through WinRM, mapped drives are not available, so the installer stages packages from `\\MORPHEUS\source\...` and can use `UITEST_SOURCE_SHARE_USERNAME` / `UITEST_SOURCE_SHARE_PASSWORD` if the source share needs separate credentials.

When `MarkUp.UITests` builds, it also builds the app project, generates the newest x64 sideload package under `MarkUp Markdown Editor\AppPackages`, and creates a dev-signing certificate under the app project's intermediate output.
The remote installer stages that package folder from the shared workspace path and runs `Add-AppDevPackage.ps1` on the remote machine.

If package installation is skipped or a custom launch target is still needed, the suite falls back to these launch targets in order:

```text
JADApps.MarkUpMarkdownEditor_yp6wg2crjc7ye!App
Z:\repos\MarkUp Markdown Editor\MarkUp Markdown Editor\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\MarkUp Markdown Editor.exe
```

Prerequisites on the remote machine:

- Developer Mode enabled
- Appium running with the Windows driver installed
- access to the shared workspace path used for package installation

If the remote machine must use a different location, set `UITEST_REMOTE_APP`, `UITEST_REMOTE_AUMID`, `UITEST_REMOTE_APP_PATH`, or `UITEST_REMOTE_SHARE_ROOT` before running the tests.

---

## Running the tests

```powershell
# Run all UI tests against the remote WinAppDriver host
dotnet test MarkUp.UITests --filter "TestCategory=UITest" -- MSTest.Parallelize.Enabled=false

# Run a specific test class
dotnet test MarkUp.UITests --filter "TestCategory=UITest&ClassName=MarkUp.UITests.FindReplaceTests"

# Skip UI tests and run unit tests only
dotnet test MarkUp.Tests
```

> **Important:** UI tests must run **sequentially** (disable parallelisation).
> The entire assembly shares a single WinAppDriver session.

> **Remote desktop visibility:** yes. WinAppDriver drives the interactive desktop session on the remote PC,
> so the app windows and UI activity are normally visible on that machine's screen while the tests run.
> If the remote session is locked or disconnected, UI automation can become unreliable.

---

## Test organisation

| File | Tests | Coverage |
|---|---|---|
| `StartupTests.cs` | 24 | Window visible, title, all UI elements present, status bar initial state |
| `EditorTypingTests.cs` | 17 | Text entry, dirty marker, status bar updates, undo/redo, formatting shortcuts |
| `MenuTests.cs` | 36 | File menu, Edit menu, all Format operations, View modes and zoom |
| `FindReplaceTests.cs` | 20 | Open/close bar, find navigation, Replace/Replace All, match case, edge cases |
| `StatusBarTests.cs` | 21 | Word/char/line counts, cursor position, zoom, toolbar buttons |
| `ViewModeTests.cs` | 17 | Editor Only / Preview Only / Split cycling, zoom boundaries, splitter drag |

---

## Architecture

```
AppSession          ← abstract base; [AssemblyInitialize] / [AssemblyCleanup]
  ├─ StartupTests
  ├─ EditorTypingTests
  ├─ MenuTests
  ├─ FindReplaceTests
  ├─ StatusBarTests
  └─ ViewModeTests
```

`AppSession` exposes:
- `Session` — the live `WindowsDriver<WindowsElement>` instance
- `FindById(automationId)` — finds elements by `AutomationProperties.AutomationId`
- `TryFindById(automationId)` — returns null instead of throwing when not found
- `ClickMenu(menuBarId, itemId)` — opens a MenuBarItem then clicks a MenuFlyoutItem
- `ClickSubMenu(menuBarId, subMenuId, itemId)` — navigates a two-level flyout
- `ResetToCleanState()` — clears the editor to a blank document before each test
- `SkipIfNoSession()` — marks test Inconclusive rather than failing when no driver

All UI element lookup uses `AutomationProperties.AutomationId` values set in
`MainWindow.xaml`. See the [AutomationId reference](#automationid-reference) below.

---

## AutomationId reference

| AutomationId | Control |
|---|---|
| `EditorTextBox` | Markdown source editor |
| `PreviewWebView` | WebView2 WYSIWYG preview |
| `SplitterBorder` | Draggable panel splitter |
| `EditorPanel` | Left editor panel |
| `PreviewPanel` | Right preview panel |
| `MenuBarFile` … `MenuBarHelp` | Top-level menu bar items |
| `MenuNew`, `MenuOpen`, `MenuSave`, … | Menu flyout items |
| `MenuHeading1` … `MenuHeading6` | Heading sub-menu items |
| `FindReplaceBar` | Find & Replace container |
| `FindTextBox` | Find input |
| `ReplaceTextBox` | Replace input |
| `FindNextButton`, `FindPrevButton` | Find navigation |
| `ReplaceButton`, `ReplaceAllButton` | Replace actions |
| `CloseFindButton` | Close Find bar |
| `FindMatchCase` | Case-sensitive toggle |
| `StatusBar` | Status bar container |
| `StatusBarStats` | Words / Characters / Lines |
| `StatusBarPosition` | Ln / Col indicator |
| `StatusBarEncoding` | Encoding label |
| `StatusBarZoom` | Zoom percentage |
| `Toolbar` | CommandBar toolbar |
| `ToolbarNew` … `ToolbarPrint` | Toolbar buttons |

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| All tests Inconclusive | Verify the remote machine can access the shared workspace path and that Appium can run `Add-AppxPackage` there |
| `SessionNotCreatedException` | Start WinAppDriver as Administrator on `192.268.0.100` |
| `NoSuchElementException` on menu items | Ensure the app is in split-view (default) before running |
| Tests hang before startup | Confirm `WinAppDriver.exe` is already running on `192.268.0.100:4723` |
| Port 4723 in use | Stop any other Appium/WinAppDriver process on the remote machine |
