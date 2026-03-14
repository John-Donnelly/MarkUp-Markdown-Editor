# UI Test Report v1

## Scope
Initial replacement wave after removing the legacy workflow-specific UI tests and introducing the new workflow-based suite.

## Suite changes in this wave
- Replaced the old fragmented UI test layout with workflow-based files:
  - `StartupTests`
  - `FileWorkflowTests`
  - `EditWorkflowTests`
  - `FormatWorkflowTests`
  - `ToolbarWorkflowTests`
  - `FindReplaceWorkflowTests`
  - `ViewWorkflowTests`
  - `BidirectionalEditingTests`
  - `RegressionWorkflowTests`
- Reworked `AppSession` to use the `SmrtPad`-style `appTopLevelWindow` attach pattern.
- Added resilient AutomationId-to-UIA-name fallback lookup for WinUI menu items.
- Added a root desktop session for dialog/picker inspection.
- Added a hidden automation bridge in `MainWindow` for preview/edit sync verification.
- Added real window-level shortcut injection and then fixed it by switching from `SendInput` to `keybd_event` after the first shortcut baseline failed.

## Executed runs in this report version
### Run 1
- `StartupTests`
- `FileWorkflowTests`
- targeted shortcut validation: `FormatWorkflowTests.Bold_ByShortcut_WrapsSelection`

## Results
### `StartupTests`
- Passed: 24
- Failed: 0

### `FileWorkflowTests`
- Passed: 11
- Failed: 9

### Targeted validation
- `FormatWorkflowTests.Bold_ByShortcut_WrapsSelection`: Passed

## Main failures observed
1. `FileMenu_Items_AreReachable("MenuExport")`
- `Export` submenu surface is still not reliably discoverable through WinUI UIA.

2. `FileMenu_Export_SubItems_AreReachable`
- `Export` submenu expansion is still brittle.

3. `New_ByMenu_ClearsEditor`
- `New` command path is still not fully deterministic in automation.

4. `New_ByToolbar_ClearsEditor`
- toolbar path needs additional stabilization.

5. `New_ByShortcut_ClearsEditor`
- shortcut path now injects keys correctly, but the command flow still needs verification.

6. `New_WhenDirty_ShowsUnsavedChangesDialog`
- unsaved-changes dialog detection is still inconsistent.

7. `SaveAs_ByMenu_OpensSystemDialog_WithoutCrashing`
- system picker detection remains too strict / too slow for the current helper.

8. `Print_ByToolbar_OpensPrintUi_WithoutCrashing`
- print UI detection remains too strict for the current helper.

9. `ExportPdf_ByMenu_IsReachable`
- export submenu navigation is still failing before the dialog assertion step.

## Fixes applied between baseline attempts before this report
- Added fallback UIA-name mappings for menu items.
- Added export-related fallback mappings.
- Replaced `SendInput` shortcut injection with `keybd_event`.
- Relaxed file-dialog assertions to allow broader desktop dialog matches.
- Isolated dirty-dialog coverage from the basic `New` command tests.

## Current diagnosis
The primary remaining blocker in the file workflow slice is WinUI `MenuFlyoutSubItem` automation reliability for the `Export` submenu, followed by dialog-surface detection for system pickers and print UI.

## Next actions for v2
1. Harden submenu navigation for `MenuExport`.
2. Stabilize `New` command verification paths.
3. Broaden desktop dialog/picker detection helpers.
4. Re-run `FileWorkflowTests` to green.
5. Run the remaining workflow classes and capture grouped failures.
