# MarkUp.UITests — End-to-End UI Test Suite

Automated UI tests for **MarkUp Markdown Editor** using [WinAppDriver](https://github.com/microsoft/WinAppDriver) (Microsoft's accessibility-based Windows UI Automation driver) and the Appium WebDriver client.

---

## Prerequisites

### 1 — WinAppDriver

Download and install **WinAppDriver 1.2.1** from:
https://github.com/microsoft/WinAppDriver/releases/tag/v1.2.1

WinAppDriver requires **Windows 10 Developer Mode** to be enabled:
- Settings → Privacy & Security → For Developers → Developer Mode: **On**

The test runner will attempt to start `WinAppDriver.exe` automatically from its default
installation path (`C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe`).
If WinAppDriver is already running, it will be used as-is.

> WinAppDriver must be started **as Administrator**. The `AssemblyInitialize` method
> launches it with the `runas` verb; accept the UAC prompt when it appears.

### 2 — Deploy the app

The tests locate the app by its **MSIX Package Family Name** using PowerShell:

```powershell
(Get-AppxPackage -Name "JADApps.MarkUpMarkdownEditor*").PackageFamilyName
```

The app must therefore be installed (sideloaded) on the test machine before running.
In Visual Studio, pressing **F5** builds and sideloads the MSIX automatically.

> If the package is not found, all tests will report **Inconclusive** and no UI
> actions will be performed — the test run will not fail the pipeline.

---

## Running the tests

```powershell
# Run all UI tests (requires WinAppDriver + installed app)
dotnet test MarkUp.UITests --filter "TestCategory=UITest" -- MSTest.Parallelize.Enabled=false

# Run a specific test class
dotnet test MarkUp.UITests --filter "TestCategory=UITest&ClassName=MarkUp.UITests.FindReplaceTests"

# Skip UI tests and run unit tests only
dotnet test MarkUp.Tests
```

> **Important:** UI tests must run **sequentially** (disable parallelisation).
> The entire assembly shares a single WinAppDriver session.

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
| All tests Inconclusive | Install app via F5 in Visual Studio, then re-run |
| `SessionNotCreatedException` | Start WinAppDriver as Administrator |
| `NoSuchElementException` on menu items | Ensure the app is in split-view (default) before running |
| Tests hang on UAC prompt | Pre-start WinAppDriver as Administrator before running tests |
| Port 4723 in use | Stop any other Appium/WinAppDriver process, or change `WinAppDriverUrl` in `AppSession.cs` |
