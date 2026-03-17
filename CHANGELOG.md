# Changelog

All notable changes to MarkUp Markdown Editor will be documented in this file.

## [1.4.5] - 2025-06-18

### Added
- **`AutomationEditorInput` injection bridge**: A hidden single-line `TextBox`
  (`AutomationEditorInput`) and companion `Button` (`AutomationSetEditorContentButton`)
  are now present in the automation bridge `Canvas`. UI tests write encoded content to the
  `TextBox`; the `EditorSyncTimer` debounces the input over ≥2 stable 150 ms ticks
  (300 ms total), decodes `|NEWLINE|` and `|HASH|` placeholders, and applies the content
  to `EditorTextBox` in a single assignment. This path is completely independent of
  keyboard layout and avoids WinUI 3 `TextBox` key-event timing issues.

### Fixed
- **UK keyboard `#` → `£` garbling in UI tests**: `PasteText()` previously called
  `SendKeys` directly on `EditorTextBox`, which routes through Appium's keyboard
  simulation layer and maps `#` to `£` on a UK layout. It now encodes `#` as `|HASH|`
  and newlines as `|NEWLINE|` and injects the content via `AutomationEditorInput`, so
  special characters arrive exactly as typed regardless of the remote machine's keyboard
  layout.
- **W3C Actions not supported by WinAppDriver**: `SendRemoteModifiedKeys()` previously
  built a `Selenium.Interactions.Actions` chain (W3C Actions protocol) which WinAppDriver
  does not implement. Modifier key shortcuts now use chord notation
  (e.g. `Keys.Control + "a"`) routed through `/element/{id}/value`, which WinAppDriver
  does support.
- **Direct `SendKeys` for editor clear in test setup**: `EditorTypingTests` and
  `StatusBarTests` `TestInitialize` methods now send `Ctrl+A` and `Delete` directly on
  the cached `Editor` element rather than through the shared `SendCtrlShortcut` /
  `SendDeleteKey` helpers, eliminating a focus-race that caused the wrong element to
  receive the keystrokes.
- **Silent remote session failure when running a single test**: `InitialiseRemoteSession`
  now performs a TCP connectivity check against the remote WinAppDriver endpoint before
  attempting package deployment or session creation. An unreachable host is diagnosed
  immediately with a clear message rather than cycling silently through all AUMID fallback
  targets.
- **App not appearing on remote screen after session creation**: `WarmUpSessionRoot()`
  now returns `bool` and the initialization loop throws `WebDriverException` if
  `EditorTextBox` does not appear within 30 seconds (previously 15 s, silent return on
  timeout). This ensures WinAppDriver session creation only succeeds when the app window
  is genuinely ready on the remote machine.
- **Stale session not reinitialized for single-test runs**: `SkipIfNoSession()` now
  triggers reinitialization when the existing session is non-null but unresponsive (e.g.
  after a previous test run closed the app), ensuring the full deployment and launch
  pipeline runs for every test regardless of how many tests are selected.

## [1.4.0] - 2025-06-17

### Added
- **Find & Replace bar**: Inline toolbar that slides in below the menu bar with a Find text
  box, Find Previous / Find Next buttons, Match Case checkbox, Replace text box, and
  Replace / Replace All buttons. All controls carry `AutomationId` attributes for reliable
  access from automated UI tests.
- **Bidirectional preview editing**: The preview pane is now `contenteditable`. Changes are
  debounced (400 ms), posted back to the host via `window.chrome.webview.postMessage`,
  converted from HTML to Markdown through `HtmlToMarkdownConverter`, and synced to the
  source editor in real time. A `_suppressNotify` flag and `updateContent()` JS function
  prevent round-trip feedback loops.
- **Focus-aware Edit menu routing**: A `FocusedPanel` enum (`None`, `Editor`, `Preview`)
  tracks which pane currently holds keyboard focus via `GotFocus`/`LostFocus` handlers.
  Undo, Redo, Cut, Copy, Paste, and Select All are all routed to the active panel — editor
  operations use the `TextBox` API; preview operations use `document.execCommand` /
  the Clipboard API inside the WebView2.
- **Setext heading support** in `MarkdownParser`: underline-style headings (`===` for H1,
  `---` for H2) are now recognised and rendered correctly alongside ATX-style headings.
- **Automation bridge panel**: A hidden 10×10 px `Canvas` (`AutomationBridgePanel`)
  positioned early in the XAML tree — before WebView2 — so WinAppDriver's UIA traversal
  reaches it without entering the Chromium accessibility subtree. Contains:
  - `AutomationFocusEditorButton` / `AutomationFocusPreviewButton` — 1×1 invisible buttons
    that programmatically set panel focus for test setup.
  - `AutomationPreviewInsertTextButton` / `AutomationPreviewBoldButton` — inject known
    content into the preview's `contenteditable` body for bidirectional-editing tests.
  - `AutomationDocumentContent`, `AutomationPreviewHtml`, `AutomationFocusedPanel`,
    `AutomationViewMode`, `AutomationLastSyncSource` — read-only `TextBlock`s that
    mirror live app state so tests can assert without querying internal fields.
- **`MarkUp.UITests` project** added to the solution: WinAppDriver + Appium (OpenQA.Selenium
  .Appium 6.x) automation test suite with 200+ tests covering startup, editor typing, all
  menu operations, Find & Replace workflows, status bar statistics, zoom, view modes, the
  splitter, and help dialogs. Supports both local WinAppDriver and remote execution against
  a second machine (configurable via `UITEST_DRIVER_URL` and `UITEST_REMOTE_APP_PATH`
  environment variables).
- **Expanded unit test coverage** — `MarkUp.Tests` grows from 151 to 288+ tests:
  - `MarkdownParserTests`: setext headings, heading slug/ID generation, inline code HTML
    escaping, `+`-prefix unordered lists, nested ordered/unordered lists, task lists,
    fenced code blocks, GFM table column alignment, blockquotes.
  - `HtmlToMarkdownConverterTests`: `<span>` bold/italic/strikethrough styles, `<div>`
    line wrapping, nested lists, task lists, table alignment separators, numeric decimal
    and hex HTML entity decoding, links, images, edge cases (empty divs, multiple `<br>`).
  - `MarkdownFormatterTests`: heading levels H3–H6, out-of-range level clamping,
    strikethrough and inline-code toggle on/off, no-selection marker insertion,
    `InsertHorizontalRule`, `InsertLink` (with and without selection), `InsertImage`.
  - `MarkdownDocumentTests`: new-document state, dirty/clean window title, `DisplayName`
    after reset, multi-change dirty tracking, `MarkSaved` cycle.
  - `DocumentStatisticsTests`: single-line counting, trailing-newline edge case, tab
    characters, `\r`/`\r\n`/`\n` mixed line endings, multi-word accuracy.
  - `DocumentExporterTests`: dark/light mode HTML output, heading conversion, plain-text
    marker stripping (bold, italic, bold-italic, code fences, image alt text, blank-line
    collapsing), null input handling.
- **`RoundTripTests` suite**: verifies Markdown → HTML → Markdown fidelity for headings,
  bold, italic, lists, blockquotes, code blocks, and GFM tables.

### Fixed
- **Ctrl+A / Copy-Paste targeting wrong panel**: Before this release, `Ctrl+A` and Paste
  always acted on the editor `TextBox` regardless of where the user had clicked. Focus is
  now tracked per panel and shortcuts are dispatched accordingly.
- **Inline code HTML escaping**: `<` and `>` inside backtick spans (e.g. `` `a < b` ``)
  were emitted as raw angle brackets, breaking HTML rendering. They are now escaped to
  `&lt;` and `&gt;` before output.
- **Table column alignment ignored**: A regex word-boundary bug in `ThCellRegex` caused
  `<th` to match the start of `<thead`, discarding any `style="text-align:center/right"`
  attributes and always producing `---` separators. The pattern now uses `\b` correctly
  so `:---:` and `---:` alignment separators are emitted as expected.
- **Incremental preview sync flicker**: Every keystroke triggered `NavigateToString`,
  resetting scroll position and causing a white flash. Subsequent updates now call
  `updateContent(escapedHtml)` via `ExecuteScriptAsync`, which replaces only the body
  `innerHTML` without a page reload.
- **Print dialog corrupting the UIA session**: Using `CoreWebView2PrintDialogKind.Browser`
  hosted the print UI inside the WebView2 renderer; dismissing the dialog triggered an
  internal back-navigation that put the WebView2 UIA provider into an unrecoverable state,
  breaking all subsequent automated tests. Switched to `CoreWebView2PrintDialogKind.System`
  which opens the native Windows print dialog in a separate OS window.
- **Nested list HTML→Markdown conversion**: `HtmlToMarkdownConverter` previously processed
  outer `<ul>`/`<ol>` first, causing inner list items to be emitted without indentation.
  Lists are now expanded innermost-first (inside-out) so child items are correctly indented
  by three spaces relative to their parent.
- **`<div>` wrapper conversion**: `contenteditable` editors wrap each line in a `<div>`;
  the new `ConvertDivs()` method maps `<div><br></div>` to blank lines and
  `<div>content</div>` to text lines, preserving paragraph structure when pasting from or
  syncing the preview.
- **`<span>` inline formatting conversion**: Inline span styles emitted by browsers
  (`font-weight:700`, `font-style:italic`, `text-decoration:line-through`) are now
  converted to `**bold**`, `*italic*`, and `~~strikethrough~~` respectively. Underline
  (`text-decoration:underline`) has no Markdown equivalent; the tag is stripped and its
  text content is preserved.
- **Strikethrough tag variants**: `<del>`, `<s>`, and `<strike>` all now convert to
  `~~text~~`; previously only `<del>` was handled.
- **Numeric HTML entity decoding**: `HtmlToMarkdownConverter` now decodes numeric decimal
  entities (`&#160;`) and numeric hex entities (`&#xA0;`) in addition to the named
  entities that were already supported.

## [1.3.2] - 2025-06-16

### Fixed
- **Line count not updating when opening a file**: `CountLines` only counted `\n` characters
  but WinUI 3's `TextBox` normalises line endings to `\r`. When the deferred `TextChanged`
  event fired after opening a file, the line count reverted to 1. Updated `CountLines` to
  recognise `\r`, `\n`, and `\r\n` as line separators. Also fixed `CountParagraphs` which
  had the same single-separator issue.
- **4 new unit tests** covering `\r`-only and `\r\n` line and paragraph counting (151 total).


## [1.3.1] - 2025-06-15

### Fixed
- **Print footer no longer shows about:blank**: Preview and print content is now served via
  virtual host URLs (`https://markup.preview/` and `https://markup.print/`) using
  `WebResourceRequested`, so the page has a real URL instead of `about:blank`.
- **PDF export footer no longer shows about:blank**: `PrintToPdfAsync` now sets `FooterUri`
  to a blank space to suppress the URL in the footer. Header title is preserved.
- **Print margins restored**: Reverted the `@page { margin: 0 }` approach. Normal print
  margins are used so the browser's header (title, date) and footer (page numbers) are
  preserved — only the about:blank URL is removed.
- **Print and PDF margins now match**: Both print and PDF export use the same default browser
  margins for consistent output.

## [1.3.0] - 2025-06-15

### Added
- **Document title in preview HTML**: `ToHtml()` now accepts a `documentTitle` parameter; the
  preview HTML includes a `<title>` tag so the document name appears in browser print headers.
- **Anchor link navigation**: Clicking `#anchor` links in the preview pane now smoothly scrolls
  to the target heading instead of being blocked.
- **Resizable split panes**: The centre splitter can be dragged left or right to resize the
  editor and preview panels. Each panel enforces a minimum width of 20%.
- **4 new unit tests** covering document title in HTML output, default title fallback, and
  anchor link scrollIntoView script presence (147 total).

### Fixed
- **Print uses browser dialog with preview**: Print now uses `ShowPrintUI(Browser)` on the
  main `PreviewWebView`, which shows the Chromium print preview dialog with full WYSIWYG
  preview. `@media print` CSS rules automatically switch to light theme and hide the toolbar.
- **Window icon**: Uses multi-resolution `.ico` file (16/32/48/256px) instead of `.png` so
  `AppWindow.SetIcon()` works correctly.

### Changed
- `MarkdownParser.ToHtml()` signature now includes optional `documentTitle` parameter.
- `MarkdownParser.BuildHtmlPage()` now emits a `<title>` tag.
- Splitter minimum width changed from fixed 100px to 20% of available width.
- Print operation no longer sets `document.title` via JavaScript (title is in HTML).

## [1.2.0] - 2025-06-14

### Added
- **Ctrl+Click to follow links**: Links in the preview pane can now be opened in the default
  browser by Ctrl+Clicking. A hover tooltip ("Ctrl+Click to follow link") appears on all links
  in both editable and non-editable modes.
- **7 new unit tests** covering link tooltip rendering, contentEditable attribute presence,
  WYSIWYG toolbar rendering, and Ctrl+Click script injection for both editable and non-editable
  preview modes.

### Fixed
- **Toolbar left-aligned**: The formatting toolbar is now left-aligned instead of stretching
  across the full window width.
- **Open dialog restricted to Markdown files**: The Open file dialog now only offers `.md` and
  `.markdown` file types, removing the previous `.txt` and `*` (all files) options.
- **Print no longer disrupts the preview panel**: Print and PDF export now use a dedicated hidden
  WebView2, so the visible preview pane is never navigated away from the current content. The
  preview panel stays exactly as-is during print and PDF operations.

### Changed
- Print and PDF export operations now use a separate background `PrintWebView` WebView2 instance.
- `MenuPrint_Click` and `MenuExportPdf_Click` no longer call `UpdatePreview()` after printing
  since the preview is never disrupted.

## [1.1.0] - 2025-06-14

### Added
- **WYSIWYG Preview Editor**: The preview pane is now a full rich-text editor. Users can edit
  directly in the rendered preview using the built-in formatting toolbar (bold, italic,
  strikethrough, headings, lists, code, links, blockquotes, horizontal rules). Changes in the
  preview are automatically converted back to Markdown and synced to the source editor.
- **HtmlToMarkdownConverter**: New core library class that converts HTML (from contentEditable)
  back to Markdown, supporting headings, bold, italic, strikethrough, inline code, code blocks
  with language, links, images, unordered lists, ordered lists, blockquotes, tables, horizontal
  rules, and paragraphs. Includes HTML entity decoding.
- **51 new unit tests** for the HtmlToMarkdownConverter, including round-trip tests that verify
  Markdown → HTML → Markdown fidelity.
- **3 new print-related unit tests** verifying document title in print output, default title
  fallback, and `!important` colour rule usage.

### Fixed
- **Print header shows correct filename**: The printed document now displays the actual document
  filename (e.g., "README.md") in the browser print header instead of "about:blank". Both
  `<title>` tag and `document.title` are set before printing. PDF export also sets the header
  title in print settings.
- **Print colour management**: Print output no longer loses text colours. All colour and
  background rules in the print stylesheet now use `!important` to prevent browser print
  overrides. Added `print-color-adjust: exact` and `-webkit-print-color-adjust: exact` to
  preserve styled backgrounds. Link colours are preserved as blue (`#0066cc`), code blocks
  retain their grey backgrounds, and table headers/cells have explicit background colours.

### Changed
- Preview panel header label changed from "PREVIEW" to "PREVIEW / EDIT" to indicate WYSIWYG
  editing capability.
- Preview WebView2 now enables context menus for standard right-click editing operations
  (cut/copy/paste) in the WYSIWYG editor.
- `MarkdownParser.ToHtml()` now accepts an optional `editable` parameter to generate
  contentEditable HTML with an embedded WYSIWYG toolbar.
- `MarkdownParser.ToHtmlForPrint()` now accepts an optional `documentTitle` parameter.

## [1.0.0] - 2025-06-13

### Added
- Initial release of MarkUp Markdown Editor.
- Split-pane Markdown editor with live HTML preview using WebView2.
- Dark mode with Mica backdrop (WinUI 3, Windows App SDK 1.8).
- Full Markdown support: headings, bold, italic, strikethrough, inline code, fenced code blocks,
  links, images, unordered/ordered/task lists, blockquotes, tables, horizontal rules.
- Menu bar and toolbar with keyboard shortcuts for all formatting operations.
- Find & Replace with case-sensitive matching.
- File operations: New, Open, Save, Save As.
- Export to HTML, Plain Text, and PDF.
- Print with print preview via WebView2.
- Font customisation dialog (font family and size).
- Zoom controls (50%–200%).
- View modes: Split, Editor Only, Preview Only.
- Word wrap toggle.
- Status bar with word/character/line count, cursor position, encoding, zoom level.
- About dialog with version, build date, runtime, architecture, and OS information.
- Markdown Quick Reference cheat sheet.
- File type associations for `.md`, `.markdown`, `.mdown`, `.mkd`.
- 80 MSTest unit tests across 5 test classes.
