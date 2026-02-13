# MarkUp Markdown Editor

A modern, dark-mode Markdown editor and viewer for Windows, built with WinUI 3 and the Windows App SDK.

## Features

- **WYSIWYG Preview Editor**: Edit directly in the rendered preview pane with a built-in formatting toolbar — changes are automatically converted back to Markdown and synced to the source editor
- **Clickable Links**: Ctrl+Click to follow links in the preview pane, with hover tooltips
- **Live Preview**: Split-pane editor with real-time rendered Markdown preview
- **Dark Mode**: Beautiful dark theme with Mica backdrop
- **Full Markdown Support**: Headings, bold, italic, strikethrough, code blocks, tables, task lists, blockquotes, images, links, and more
- **Formatting Toolbar**: Left-aligned quick-access toolbar buttons and keyboard shortcuts for all formatting operations
- **Find & Replace**: Built-in find and replace with case-sensitive matching
- **Print & Export**: Clean printing with document title header and page numbers — no about:blank in footers; print to PDF, export to HTML, and export to plain text with proper font colour management
- **Font Customization**: Configurable editor font family and size
- **Zoom Controls**: Zoom in/out on the editor
- **View Modes**: Switch between split view, editor-only, and preview-only
- **Word Wrap Toggle**: Enable or disable word wrapping in the editor
- **Status Bar**: Live word count, character count, line count, cursor position, and zoom level
- **File Associations**: Registered as a handler for `.md`, `.markdown`, `.mdown`, `.mkd` files
- **About Dialog**: Displays version, build date, runtime, architecture, and OS information
- **Markdown Quick Reference**: Built-in cheat sheet accessible from the Help menu
- **Keyboard Shortcuts**: Standard shortcuts for all common operations

## Keyboard Shortcuts

| Action | Shortcut |
|--------|----------|
| New | Ctrl+N |
| Open | Ctrl+O |
| Save | Ctrl+S |
| Save As | Ctrl+Shift+S |
| Print | Ctrl+P |
| Undo | Ctrl+Z |
| Redo | Ctrl+Y |
| Cut | Ctrl+X |
| Copy | Ctrl+C |
| Paste | Ctrl+V |
| Select All | Ctrl+A |
| Find & Replace | Ctrl+H |
| Bold | Ctrl+B |
| Italic | Ctrl+I |
| Inline Code | Ctrl+E |
| Insert Link | Ctrl+K |
| Follow Link | Ctrl+Click |
| Zoom In | Ctrl++ |
| Zoom Out | Ctrl+- |

## Architecture

- **MarkUp Markdown Editor** — WinUI 3 application (main project)
- **MarkUp.Core** — Class library with markdown parsing, formatting, document model, HTML-to-Markdown conversion, and export logic
- **MarkUp.Tests** — MSTest unit tests for the core library (147 tests)

## Building

Requires:
- .NET 8.0 SDK
- Windows App SDK 1.8
- Visual Studio 2022 or later with WinUI workload

## License

© 2025 John Donnelly. All rights reserved.
