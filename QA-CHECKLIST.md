# MarkUp Markdown Editor — Pre-Submission QA Checklist

Run this top to bottom before every Microsoft Store submission. Companion to
`PACKAGING.md` (build/upload mechanics) and the release checklist in the repo
notes. Items marked **[regression]** guard bugs that have actually shipped or
nearly shipped — do not skip them.

---

## 1 — Version & metadata

- [ ] `<Version>` bumped in `MarkUp Markdown Editor.csproj` (`X.Y.Z`)
- [ ] `Version` bumped in `Package.appxmanifest` (`X.Y.Z.0`) — must match csproj
- [ ] `CHANGELOG.md`: `## [X.Y.Z] - YYYY-MM-DD` section added using the actual
      commit date (`git log -1 --format=%ad --date=short`); no lingering
      `[Unreleased]` items that shipped
- [ ] `README.md` feature list and shortcut table match what the build actually does
- [ ] Production `jad-apps-site-git` (JAD Apps site repository): `data/news.json` item added,
      `data/sync-state.json` advanced, `data/studio-projects.ts` refreshed (`app.js` no longer
      exists). Do not update the stale `jad-apps-site` Pages project.
- [ ] About dialog shows the new version (auto-derived, but eyeball it)

## 2 — Automated verification

- [ ] Unit tests green: `dotnet test MarkUp.Tests/MarkUp.Tests.csproj`
- [ ] UI tests green — remote host (`Setup-RemoteUiTestHost.ps1`) or local mode
      (`UITEST_DRIVER_URL` recipe in `MarkUp.UITests/README.md`).
      `BidirectionalEditingTests` is `[Ignore]`d by design; everything else must pass
- [ ] **[regression]** Build-order gotcha: building `MarkUp.UITests` regenerates the
      app exe *without* self-contained flags. If you smoke-test the loose exe, rebuild
      the app with `-p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true`
      *after* any UITests build, or the exe crashes at startup (REGDB_E_CLASSNOTREG)
- [ ] App builds with **0 warnings**: `dotnet build "MarkUp Markdown Editor/MarkUp
      Markdown Editor.csproj" -c Debug -p:Platform=x64`
- [ ] No orphan `MarkUp Markdown Editor.exe` processes locking build output
      (kill via `Stop-Process`, not Git Bash `kill`)

## 3 — Editor pane (manual)

- [ ] Type a paragraph; characters appear with no lag; caret behaves normally
- [ ] **[regression]** Type a bare `#` on its own line, then click around / keep
      typing — app must NOT freeze (selection-projection infinite loop)
- [ ] Type `#nospace` and `##` — render as literal text in the preview, no freeze
- [ ] Undo (Ctrl+Z) / Redo (Ctrl+Y) through a multi-edit sequence
- [ ] Word wrap toggle works and the menu item's check mark tracks it
- [ ] Spell-check squiggles appear on misspelled words
- [ ] Type `- item`, Enter — next line starts with `- `; Enter on the empty item removes
      the marker. Same for `1.` (increments), `- [ ]`, `>`. Shift+Enter gives a plain newline
- [ ] Tab on a list line indents by two spaces and focus STAYS in the editor; Shift+Tab
      outdents; Tab on plain text inserts two spaces
- [ ] **[regression]** Ctrl+B on a selection, then Ctrl+Z — the bold markers come off and
      the text remains (undo survives formatting, paste, replace and list edits)
- [ ] Open an LF file, type a line, save — file still LF only (no bare CR); CRLF file stays CRLF

## 4 — Keyboard shortcuts (manual, editor focused)

- [ ] Ctrl+B wraps selection in `**`; pressing again unwraps
- [ ] **[regression]** Ctrl+I wraps selection in `*` and does NOT insert a tab
      character or delete the selection
- [ ] **[regression]** Ctrl+H opens Find & Replace (exactly one toggle per press);
      Ctrl+F does the same; Escape in the find box closes it
- [ ] Ctrl+E inline code, Ctrl+K link, Ctrl+N/O/S/Shift+S file ops, Ctrl+P print
- [ ] Zoom: Ctrl+= / Ctrl+- on the MAIN keyboard row, numpad +/-, and Ctrl+0 reset

## 5 — Pane parity (manual, split view)

- [ ] Scroll the editor — preview follows proportionally; scroll the preview —
      editor follows; no ping-pong oscillation when either pane rests
- [ ] View ▸ Synchronized Scrolling unchecked stops both directions; recheck restores
- [ ] Click around the editor — a blinking caret marker appears at the matching
      spot in the preview; moves as the caret moves
- [ ] Select text in the editor — the same visible range highlights in the preview
      (markdown delimiters excluded); collapse selection — highlight clears, caret
      marker returns
- [ ] Select rendered text in the preview — the editor selection mirrors it
      (delimiters included for full-token selections)
- [ ] With the editor focused, move the caret to text far off-screen in the preview
      — the preview scrolls it into view
- [ ] Zoom in/out — BOTH panes scale together; status bar percentage updates;
      caret marker still lands on the right spot while zoomed

## 6 — WYSIWYG preview editing (manual)

- [ ] Click into the preview, type — markdown source updates in the editor
- [ ] Bold/italic via toolbar while the preview is focused formats the right range
- [ ] Editing in the preview does not reset its scroll position or cursor
- [ ] Ctrl+Click a link opens the browser; plain click does nothing; `#anchor`
      links scroll smoothly
- [ ] Paste rich text from a browser AND from Word into the editor — arrives as
      clean Markdown (no `<b>` tags, no `·` bullet glyphs, lists nested correctly)
- [ ] Paste plain text that LOOKS like HTML (`<b>hi</b>`) — inserted literally,
      not converted
- [ ] **[regression]** Ctrl+V (not just Edit ▸ Paste) with rich text on the clipboard
      arrives as Markdown in the editor
- [ ] Click a task checkbox in the preview — the `[ ]`/`[x]` flips in the source and the rest
      of the document is untouched; undo reverts it
- [ ] Open a saved document with a relative image (`![](img.png)` next to the file) — the
      image renders in the preview and in PDF export
- [ ] Type in the editor and click into the preview within half a second, then type in the
      preview — the editor keeps the characters typed before the click
- [ ] Multi-block document (heading, paragraphs, list, task list, table): select a word in
      the LAST block in the editor — the preview highlights exactly that word, not a
      neighbour; select it in the preview — the editor selects exactly it

## 7 — Find & Replace (manual)

- [ ] Match count updates live while typing the search term and when toggling
      match case
- [ ] Enter = next, Shift+Enter = previous; both wrap around the document
- [ ] Replace replaces current match and advances; Replace All replaces every match
- [ ] Case-sensitive search honors the checkbox

## 8 — Files & persistence (manual)

- [ ] New/Open with a dirty document prompts Save / Don't Save / Cancel
- [ ] **[regression]** Exit menu, Alt+F4, AND the title-bar X all prompt when dirty;
      Cancel keeps the window open; saving from the prompt then closes
- [ ] Open dialog lists `.md .markdown .mdown .mkd .txt`; each opens correctly
- [ ] Double-click association: `.md`, `.markdown`, `.mdown`, `.mkd` files open in
      MarkUp (test on the *installed package*, not the loose exe)
- [ ] Save / Save As round-trips content byte-identically (line endings preserved)
- [ ] Settings persist across restart: font family/size, zoom, view mode, word
      wrap, sync scrolling, status-bar visibility, window size
- [ ] Delete `%LocalAppData%\MarkUp\settings.json` — app starts clean with defaults

## 9 — Export & print (manual)

- [ ] Export HTML: opens in a browser, light theme, correct rendering
- [ ] Export plain text: **[regression]** code-fence content with `**` / `~~`
      survives verbatim; emphasis markers stripped elsewhere; `snake_case` intact
- [ ] Export PDF with a document containing: a WIDE table, a LONG code block
      (>1 page), long unbroken strings — **[regression]** everything wraps, nothing
      is clipped, code blocks and tables continue across pages
- [ ] File ▸ Print (paper or Microsoft Print to PDF): same wrap checks; light
      colors on paper (no dark backgrounds); document title in header, page
      numbers present, no `about:blank` in the footer

## 10 — Stability & performance (manual)

- [ ] Load a large document (500+ KB); typing stays responsive; preview updates
      within ~half a second of pausing
- [ ] **[regression]** After a full manual session, check Event Viewer ▸ Windows
      Logs ▸ Application for `MarkUp Markdown Editor.exe` entries (Application
      Error 1000 / .NET Runtime 1026) — there must be none from your session.
      Watch especially for 0xc000027b (stowed exception = unobserved WinRT async)
- [ ] Rapid typing while the preview renders — no dropped final state (preview
      matches the editor once idle)
- [ ] Resize the window, drag the splitter, switch view modes repeatedly — layout
      stays sane; splitter ratio survives a resize

## 11 — Store package

- [ ] Build the multi-arch Release bundle per `PACKAGING.md`
      (`-p:Platform=x64`, `UapAppxPackageBuildMode=StoreUpload`, `AppxBundle=Always`,
      `x86|x64|ARM64`) — `PublishTrimmed` must remain `False`; ReadyToRun is
      auto-disabled for bundles (NETSDK1094 guard). Use the generated `*_bundle.msixupload`
      for Partner Center; do not upload the dev-signed `_Test` bundle.
- [ ] Install the `.msixbundle` fresh on a machine (or after uninstalling the
      dev build) and smoke-test: launch, type, preview, save, print preview
- [ ] Packaged app icon, display name, and file associations correct
- [ ] Upload the generated `*_bundle.msixupload` to Partner Center. It contains the
      `.msixbundle` and per-architecture `.appxsym` symbol files.
- [ ] Listing screenshots still representative (retake if the UI changed visibly)

## 12 — After submission

- [ ] Tag: `git tag vX.Y.Z && git push origin vX.Y.Z`
- [ ] Certification result checked within 48h; test the Store-delivered build
      on at least one machine once published
