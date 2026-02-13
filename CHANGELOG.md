# Changelog

All notable changes to MarkUp Markdown Editor will be documented in this file.

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
- **Print footer removed**: Added `@page { margin: 0 }` CSS rule in print media query, which
  eliminates the browser's header/footer area entirely (removing the `about:blank` footer).
  Body padding of 15mm preserves content margins on paper.
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
