using MarkUp.Core;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MarkUp_Markdown_Editor;

public sealed partial class MainWindow : Window
{
    private readonly MarkdownDocument _document = new();
    private readonly DispatcherTimer _previewTimer;
    private bool _suppressTextChanged;
    private bool _suppressPreviewSync;
    private bool _webViewReady;
    private bool _printWebViewReady;
    private int _zoomPercent = 100;
    private bool _isSplitterDragging;
    private double _splitterStartX;
    private double _editorStartWidth;

    // View modes
    private enum ViewMode { Split, EditorOnly, PreviewOnly }
    private ViewMode _viewMode = ViewMode.Split;

    public MainWindow()
    {
        InitializeComponent();

        // Set window icon
        SetWindowIcon();

        // Set minimum window size and reasonable default
        SetWindowSize(1280, 800);

        // Set up debounced preview timer
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _previewTimer.Tick += PreviewTimer_Tick;

        // Initialize WebView2
        InitializeWebViewAsync();
    }

    private void SetWindowIcon()
    {
        try
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "MarkUp-icon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch
        {
            // Icon setting is optional; don't crash
        }
    }

    private void SetWindowSize(int width, int height)
    {
        try
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(width, height));
        }
        catch
        {
            // Sizing is optional
        }
    }

    private async void InitializeWebViewAsync()
    {
        try
        {
            await PreviewWebView.EnsureCoreWebView2Async();
            _webViewReady = true;
            PreviewWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            PreviewWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            PreviewWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            PreviewWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            UpdatePreview();
        }
        catch
        {
            // WebView2 runtime may not be installed
        }

        try
        {
            await PrintWebView.EnsureCoreWebView2Async();
            _printWebViewReady = true;
            PrintWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            PrintWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        }
        catch
        {
            // Print WebView2 initialization is optional
        }
    }

    private void CoreWebView2_WebMessageReceived(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var messageJson = args.WebMessageAsJson;
            if (string.IsNullOrEmpty(messageJson)) return;

            // Handle link open request: { "type": "openLink", "url": "..." }
            if (messageJson.Contains("openLink"))
            {
                var urlStartMarker = "\"url\":\"";
                var urlStart = messageJson.IndexOf(urlStartMarker);
                if (urlStart >= 0)
                {
                    urlStart += urlStartMarker.Length;
                    var urlEnd = messageJson.IndexOf('"', urlStart);
                    if (urlEnd > urlStart)
                    {
                        var url = messageJson[urlStart..urlEnd]
                            .Replace("\\\"", "\"")
                            .Replace("\\/", "/")
                            .Replace("\\\\", "\\");
                        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                            (uri.Scheme == "http" || uri.Scheme == "https"))
                        {
                            _ = Windows.System.Launcher.LaunchUriAsync(uri);
                        }
                    }
                }
                return;
            }

            // Handle content changed: { "type": "contentChanged", "html": "..." }
            if (!messageJson.Contains("contentChanged")) return;

            var htmlStartMarker = "\"html\":\"";
            var htmlStart = messageJson.IndexOf(htmlStartMarker);
            if (htmlStart < 0) return;

            htmlStart += htmlStartMarker.Length;
            var htmlEnd = messageJson.LastIndexOf('"');
            if (htmlEnd <= htmlStart) return;

            var htmlContent = messageJson[htmlStart..htmlEnd];
            // Unescape JSON string
            htmlContent = htmlContent
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\/", "/")
                .Replace("\\\\", "\\");

            var markdown = HtmlToMarkdownConverter.Convert(htmlContent);

            // Sync back to editor
            _suppressPreviewSync = true;
            _suppressTextChanged = true;
            EditorTextBox.Text = markdown;
            _suppressTextChanged = false;
            _document.Content = markdown;
            _suppressPreviewSync = false;
            UpdateTitle();
            UpdateStatusBar();
        }
        catch
        {
            // Ignore parsing errors from WYSIWYG sync
        }
    }

    private void UpdateTitle()
    {
        Title = _document.GetWindowTitle();
    }

    #region Editor Events

    private void EditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged) return;

        _document.Content = EditorTextBox.Text;
        UpdateTitle();
        UpdateStatusBar();

        // Debounce preview updates
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void EditorTextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateCursorPosition();
    }

    private void PreviewTimer_Tick(object? sender, object e)
    {
        _previewTimer.Stop();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (!_webViewReady) return;
        if (_suppressPreviewSync) return;

        try
        {
            var html = MarkdownParser.ToHtml(_document.Content, darkMode: true, editable: true, documentTitle: _document.DisplayName);
            PreviewWebView.NavigateToString(html);
        }
        catch
        {
            // Swallow rendering errors
        }
    }

    private void UpdateStatusBar()
    {
        var stats = _document.GetStatistics();
        StatusBarStats.Text = stats.ToString();
    }

    private void UpdateCursorPosition()
    {
        var text = EditorTextBox.Text;
        var pos = EditorTextBox.SelectionStart;

        int line = 1, col = 1;
        for (int i = 0; i < pos && i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                line++;
                col = 1;
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
            else if (text[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }
        }
        StatusBarPosition.Text = $"Ln {line}, Col {col}";
    }

    #endregion

    #region File Menu

    private async void MenuNew_Click(object sender, RoutedEventArgs e)
    {
        if (_document.IsDirty)
        {
            var save = await ShowSavePromptAsync();
            if (save == ContentDialogResult.Primary)
            {
                await SaveDocumentAsync();
            }
            else if (save == ContentDialogResult.None)
            {
                return; // Cancelled
            }
        }

        _document.Reset();
        _suppressTextChanged = true;
        EditorTextBox.Text = string.Empty;
        _suppressTextChanged = false;
        UpdateTitle();
        UpdateStatusBar();
        UpdatePreview();
    }

    private async void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_document.IsDirty)
        {
            var save = await ShowSavePromptAsync();
            if (save == ContentDialogResult.Primary)
            {
                await SaveDocumentAsync();
            }
            else if (save == ContentDialogResult.None)
            {
                return;
            }
        }

        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".markdown");

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            var content = await File.ReadAllTextAsync(file.Path);
            _document.Reset();
            _document.FilePath = file.Path;
            _suppressTextChanged = true;
            EditorTextBox.Text = content;
            _suppressTextChanged = false;
            _document.Content = content;
            _document.MarkSaved();
            UpdateTitle();
            UpdateStatusBar();
            UpdatePreview();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Open Failed", $"Could not open the file.\n{ex.Message}");
        }
    }

    private async void MenuSave_Click(object sender, RoutedEventArgs e)
    {
        await SaveDocumentAsync();
    }

    private async void MenuSaveAs_Click(object sender, RoutedEventArgs e)
    {
        await SaveDocumentAsAsync();
    }

    private async Task<bool> SaveDocumentAsync()
    {
        if (string.IsNullOrEmpty(_document.FilePath))
        {
            return await SaveDocumentAsAsync();
        }

        try
        {
            await File.WriteAllTextAsync(_document.FilePath, _document.Content);
            _document.MarkSaved();
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Save Failed", $"Could not save the file.\n{ex.Message}");
            return false;
        }
    }

    private async Task<bool> SaveDocumentAsAsync()
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Markdown", new[] { ".md" });
        picker.FileTypeChoices.Add("Text", new[] { ".txt" });
        picker.SuggestedFileName = _document.DisplayName == "Untitled" ? "document" : Path.GetFileNameWithoutExtension(_document.DisplayName);

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file == null) return false;

        try
        {
            await File.WriteAllTextAsync(file.Path, _document.Content);
            _document.FilePath = file.Path;
            _document.MarkSaved();
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Save Failed", $"Could not save the file.\n{ex.Message}");
            return false;
        }
    }

    private async void MenuExportHtml_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("HTML", new[] { ".html" });
        picker.SuggestedFileName = "export";

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            var html = DocumentExporter.ExportToHtml(_document.Content, darkMode: false);
            await File.WriteAllTextAsync(file.Path, html);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Export Failed", ex.Message);
        }
    }

    private async void MenuExportPlainText_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Text", new[] { ".txt" });
        picker.SuggestedFileName = "export";

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            var plainText = DocumentExporter.ExportToPlainText(_document.Content);
            await File.WriteAllTextAsync(file.Path, plainText);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Export Failed", ex.Message);
        }
    }

    private async void MenuExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (!_printWebViewReady)
        {
            await ShowErrorAsync("PDF Export", "Print engine is not ready. Please wait and try again.");
            return;
        }

        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("PDF", new[] { ".pdf" });
        picker.SuggestedFileName = string.IsNullOrEmpty(_document.FilePath) ? "document" : Path.GetFileNameWithoutExtension(_document.FilePath);

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            var printHtml = MarkdownParser.ToHtmlForPrint(_document.Content, _document.DisplayName);

            // Navigate and wait for the page to fully load before exporting
            var navigationTcs = new TaskCompletionSource<bool>();
            void OnNavigationCompleted(WebView2 s, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs a)
            {
                navigationTcs.TrySetResult(a.IsSuccess);
            }
            PrintWebView.NavigationCompleted += OnNavigationCompleted;
            PrintWebView.NavigateToString(printHtml);
            var navSuccess = await navigationTcs.Task;
            PrintWebView.NavigationCompleted -= OnNavigationCompleted;

            if (!navSuccess)
            {
                await ShowErrorAsync("PDF Export", "Failed to prepare content for PDF export.");
                return;
            }

            var printSettings = PrintWebView.CoreWebView2.Environment.CreatePrintSettings();
            printSettings.ShouldPrintBackgrounds = true;
            printSettings.HeaderTitle = _document.DisplayName;
            printSettings.ShouldPrintHeaderAndFooter = true;

            var success = await PrintWebView.CoreWebView2.PrintToPdfAsync(file.Path, printSettings);

            if (!success)
            {
                await ShowErrorAsync("PDF Export", "PDF export failed. Please try again.");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("PDF Export Failed", ex.Message);
        }
    }

    private async void MenuPrint_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewReady)
        {
            await ShowErrorAsync("Print", "Preview is not ready. Please wait and try again.");
            return;
        }

        try
        {
            // The preview HTML already has the document title in its <title> tag,
            // and @media print CSS rules switch to light theme and hide the toolbar.
            PreviewWebView.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Print Failed", ex.Message);
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Edit Menu

    private void MenuUndo_Click(object sender, RoutedEventArgs e)
    {
        EditorTextBox.Undo();
    }

    private void MenuRedo_Click(object sender, RoutedEventArgs e)
    {
        EditorTextBox.Redo();
    }

    private void MenuCut_Click(object sender, RoutedEventArgs e)
    {
        if (EditorTextBox.SelectionLength > 0)
        {
            var dp = new DataPackage();
            dp.SetText(EditorTextBox.SelectedText);
            Clipboard.SetContent(dp);

            var start = EditorTextBox.SelectionStart;
            var text = EditorTextBox.Text;
            EditorTextBox.Text = text.Remove(start, EditorTextBox.SelectionLength);
            EditorTextBox.SelectionStart = start;
        }
    }

    private void MenuCopy_Click(object sender, RoutedEventArgs e)
    {
        if (EditorTextBox.SelectionLength > 0)
        {
            var dp = new DataPackage();
            dp.SetText(EditorTextBox.SelectedText);
            Clipboard.SetContent(dp);
        }
    }

    private async void MenuPaste_Click(object sender, RoutedEventArgs e)
    {
        var content = Clipboard.GetContent();
        if (content.Contains(StandardDataFormats.Text))
        {
            var text = await content.GetTextAsync();
            var start = EditorTextBox.SelectionStart;
            var fullText = EditorTextBox.Text;
            var newText = fullText.Remove(start, EditorTextBox.SelectionLength).Insert(start, text);
            _suppressTextChanged = true;
            EditorTextBox.Text = newText;
            EditorTextBox.SelectionStart = start + text.Length;
            _suppressTextChanged = false;
            _document.Content = EditorTextBox.Text;
            UpdateTitle();
            UpdateStatusBar();
            _previewTimer.Stop();
            _previewTimer.Start();
        }
    }

    private void MenuSelectAll_Click(object sender, RoutedEventArgs e)
    {
        EditorTextBox.SelectAll();
    }

    private void MenuFind_Click(object sender, RoutedEventArgs e)
    {
        FindReplaceBar.Visibility = FindReplaceBar.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (FindReplaceBar.Visibility == Visibility.Visible)
        {
            FindTextBox.Focus(FocusState.Programmatic);
        }
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        FindInEditor(forward: true);
    }

    private void FindPrev_Click(object sender, RoutedEventArgs e)
    {
        FindInEditor(forward: false);
    }

    private void FindInEditor(bool forward)
    {
        var searchText = FindTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var text = EditorTextBox.Text;
        var comparison = FindMatchCase.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        int startPos;
        int index;

        if (forward)
        {
            startPos = EditorTextBox.SelectionStart + EditorTextBox.SelectionLength;
            index = text.IndexOf(searchText, startPos, comparison);
            if (index < 0)
                index = text.IndexOf(searchText, 0, comparison); // wrap
        }
        else
        {
            startPos = EditorTextBox.SelectionStart;
            if (startPos > 0)
                index = text.LastIndexOf(searchText, startPos - 1, comparison);
            else
                index = text.LastIndexOf(searchText, text.Length - 1, comparison);
        }

        if (index >= 0)
        {
            EditorTextBox.SelectionStart = index;
            EditorTextBox.SelectionLength = searchText.Length;
            EditorTextBox.Focus(FocusState.Programmatic);
        }
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        var searchText = FindTextBox.Text;
        var replaceText = ReplaceTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var comparison = FindMatchCase.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        if (EditorTextBox.SelectionLength > 0 &&
            EditorTextBox.SelectedText.Equals(searchText, comparison))
        {
            var start = EditorTextBox.SelectionStart;
            var text = EditorTextBox.Text;
            var newText = text.Remove(start, EditorTextBox.SelectionLength).Insert(start, replaceText);
            EditorTextBox.Text = newText;
            EditorTextBox.SelectionStart = start + replaceText.Length;
        }

        FindInEditor(forward: true);
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        var searchText = FindTextBox.Text;
        var replaceText = ReplaceTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var text = EditorTextBox.Text;
        var comparison = FindMatchCase.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var newText = text.Replace(searchText, replaceText, comparison);
        if (newText != text)
        {
            EditorTextBox.Text = newText;
        }
    }

    private void CloseFindBar_Click(object sender, RoutedEventArgs e)
    {
        FindReplaceBar.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Format Menu

    private void ApplyFormatting(Func<string, int, int, FormattingResult> formatter)
    {
        var text = EditorTextBox.Text;
        var selStart = EditorTextBox.SelectionStart;
        var selLen = EditorTextBox.SelectionLength;

        var result = formatter(text, selStart, selLen);

        _suppressTextChanged = true;
        EditorTextBox.Text = result.NewText;
        EditorTextBox.SelectionStart = result.NewSelectionStart;
        EditorTextBox.SelectionLength = result.NewSelectionLength;
        _suppressTextChanged = false;

        _document.Content = EditorTextBox.Text;
        UpdateTitle();
        UpdateStatusBar();
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void ApplyLineFormatting(Func<string, int, FormattingResult> formatter)
    {
        var text = EditorTextBox.Text;
        var selStart = EditorTextBox.SelectionStart;

        var result = formatter(text, selStart);

        _suppressTextChanged = true;
        EditorTextBox.Text = result.NewText;
        EditorTextBox.SelectionStart = result.NewSelectionStart;
        EditorTextBox.SelectionLength = result.NewSelectionLength;
        _suppressTextChanged = false;

        _document.Content = EditorTextBox.Text;
        UpdateTitle();
        UpdateStatusBar();
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void MenuBold_Click(object sender, RoutedEventArgs e)
        => ApplyFormatting(MarkdownFormatter.ToggleBold);

    private void MenuItalic_Click(object sender, RoutedEventArgs e)
        => ApplyFormatting(MarkdownFormatter.ToggleItalic);

    private void MenuStrikethrough_Click(object sender, RoutedEventArgs e)
        => ApplyFormatting(MarkdownFormatter.ToggleStrikethrough);

    private void MenuInlineCode_Click(object sender, RoutedEventArgs e)
        => ApplyFormatting(MarkdownFormatter.ToggleInlineCode);

    private void InsertHeading(int level)
    {
        var text = EditorTextBox.Text;
        var selStart = EditorTextBox.SelectionStart;
        var result = MarkdownFormatter.InsertHeading(text, selStart, level);

        _suppressTextChanged = true;
        EditorTextBox.Text = result.NewText;
        EditorTextBox.SelectionStart = result.NewSelectionStart;
        _suppressTextChanged = false;

        _document.Content = EditorTextBox.Text;
        UpdateTitle();
        UpdateStatusBar();
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void MenuHeading1_Click(object sender, RoutedEventArgs e) => InsertHeading(1);
    private void MenuHeading2_Click(object sender, RoutedEventArgs e) => InsertHeading(2);
    private void MenuHeading3_Click(object sender, RoutedEventArgs e) => InsertHeading(3);
    private void MenuHeading4_Click(object sender, RoutedEventArgs e) => InsertHeading(4);
    private void MenuHeading5_Click(object sender, RoutedEventArgs e) => InsertHeading(5);
    private void MenuHeading6_Click(object sender, RoutedEventArgs e) => InsertHeading(6);

    private void MenuUnorderedList_Click(object sender, RoutedEventArgs e)
        => ApplyLineFormatting(MarkdownFormatter.InsertUnorderedList);

    private void MenuOrderedList_Click(object sender, RoutedEventArgs e)
        => ApplyLineFormatting(MarkdownFormatter.InsertOrderedList);

    private void MenuTaskList_Click(object sender, RoutedEventArgs e)
        => ApplyLineFormatting(MarkdownFormatter.InsertTaskList);

    private void MenuBlockquote_Click(object sender, RoutedEventArgs e)
        => ApplyLineFormatting(MarkdownFormatter.InsertBlockquote);

    private void MenuCodeBlock_Click(object sender, RoutedEventArgs e)
        => ApplyFormatting(MarkdownFormatter.InsertCodeBlock);

    private void MenuHorizontalRule_Click(object sender, RoutedEventArgs e)
        => ApplyLineFormatting(MarkdownFormatter.InsertHorizontalRule);

    private void MenuInsertLink_Click(object sender, RoutedEventArgs e)
        => ApplyFormatting(MarkdownFormatter.InsertLink);

    private void MenuInsertImage_Click(object sender, RoutedEventArgs e)
        => ApplyFormatting(MarkdownFormatter.InsertImage);

    private void MenuInsertTable_Click(object sender, RoutedEventArgs e)
    {
        var text = EditorTextBox.Text;
        var selStart = EditorTextBox.SelectionStart;
        var result = MarkdownFormatter.InsertTable(text, selStart, 3, 3);

        _suppressTextChanged = true;
        EditorTextBox.Text = result.NewText;
        EditorTextBox.SelectionStart = result.NewSelectionStart;
        _suppressTextChanged = false;

        _document.Content = EditorTextBox.Text;
        UpdateTitle();
        UpdateStatusBar();
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    #endregion

    #region View Menu

    private void SetViewMode(ViewMode mode)
    {
        _viewMode = mode;
        switch (mode)
        {
            case ViewMode.EditorOnly:
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                SplitterColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(0);
                SplitterBorder.Visibility = Visibility.Collapsed;
                PreviewPanel.Visibility = Visibility.Collapsed;
                EditorPanel.Visibility = Visibility.Visible;
                break;

            case ViewMode.PreviewOnly:
                EditorColumn.Width = new GridLength(0);
                SplitterColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                SplitterBorder.Visibility = Visibility.Collapsed;
                EditorPanel.Visibility = Visibility.Collapsed;
                PreviewPanel.Visibility = Visibility.Visible;
                break;

            case ViewMode.Split:
            default:
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                SplitterColumn.Width = GridLength.Auto;
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                SplitterBorder.Visibility = Visibility.Visible;
                EditorPanel.Visibility = Visibility.Visible;
                PreviewPanel.Visibility = Visibility.Visible;
                break;
        }
    }

    private void MenuViewEditor_Click(object sender, RoutedEventArgs e) => SetViewMode(ViewMode.EditorOnly);
    private void MenuViewPreview_Click(object sender, RoutedEventArgs e) => SetViewMode(ViewMode.PreviewOnly);
    private void MenuViewSplit_Click(object sender, RoutedEventArgs e) => SetViewMode(ViewMode.Split);

    private void MenuToggleWordWrap_Click(object sender, RoutedEventArgs e)
    {
        EditorTextBox.TextWrapping = EditorTextBox.TextWrapping == TextWrapping.Wrap
            ? TextWrapping.NoWrap
            : TextWrapping.Wrap;
    }

    private void MenuZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _zoomPercent = Math.Min(200, _zoomPercent + 10);
        ApplyZoom();
    }

    private void MenuZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _zoomPercent = Math.Max(50, _zoomPercent - 10);
        ApplyZoom();
    }

    private void MenuZoomReset_Click(object sender, RoutedEventArgs e)
    {
        _zoomPercent = 100;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        EditorTextBox.FontSize = 14 * (_zoomPercent / 100.0);
        StatusBarZoom.Text = $"{_zoomPercent}%";
    }

    private void MenuToggleStatusBar_Click(object sender, RoutedEventArgs e)
    {
        StatusBar.Visibility = StatusBar.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    #endregion

    #region Font Settings

    private async void ToolbarFontSettings_Click(object sender, RoutedEventArgs e)
    {
        var fontSizeBox = new NumberBox
        {
            Header = "Font Size",
            Value = EditorTextBox.FontSize,
            Minimum = 8,
            Maximum = 72,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };

        var fontFamilyBox = new ComboBox
        {
            Header = "Font Family",
            Width = 280,
            Items =
            {
                "Cascadia Code",
                "Cascadia Mono",
                "Consolas",
                "Courier New",
                "Segoe UI",
                "Lucida Console",
                "Fira Code"
            },
            SelectedItem = EditorTextBox.FontFamily.Source
        };

        // Default selection if current font not in list
        if (fontFamilyBox.SelectedIndex < 0)
            fontFamilyBox.SelectedIndex = 0;

        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(fontFamilyBox);
        panel.Children.Add(fontSizeBox);

        var dialog = new ContentDialog
        {
            Title = "Font Settings",
            Content = panel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (fontFamilyBox.SelectedItem is string fontFamily)
            {
                EditorTextBox.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(fontFamily);
            }
            EditorTextBox.FontSize = fontSizeBox.Value;
        }
    }

    #endregion

    #region Help Menu

    private async void MenuMarkdownRef_Click(object sender, RoutedEventArgs e)
    {
        var referenceText = @"# Markdown Quick Reference

## Headings
# H1  ## H2  ### H3  #### H4  ##### H5  ###### H6

## Emphasis
**bold**  *italic*  ***bold italic***  ~~strikethrough~~

## Lists
- Bullet item (- or * or +)
1. Numbered item

## Task Lists
- [x] Completed
- [ ] Incomplete

## Links & Images
[Link text](url)
![Alt text](image-url)

## Code
`inline code`
```language
code block
```

## Blockquotes
> Quoted text

## Tables
| Header 1 | Header 2 |
| -------- | -------- |
| Cell 1   | Cell 2   |

## Horizontal Rule
---";

        var dialog = new ContentDialog
        {
            Title = "Markdown Quick Reference",
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = referenceText,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas"),
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                },
                MaxHeight = 500
            },
            CloseButtonText = "Close",
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version ?? new Version(1, 0, 0);
        var buildDate = File.GetLastWriteTime(assembly.Location);
        var runtimeVersion = RuntimeInformation.FrameworkDescription;
        var osVersion = RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.ProcessArchitecture;

        var aboutPanel = new StackPanel { Spacing = 12 };

        // App icon and title
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "MarkUp-icon.png");
            if (File.Exists(iconPath))
            {
                var icon = new Microsoft.UI.Xaml.Controls.Image
                {
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath)),
                    Width = 64,
                    Height = 64
                };
                headerPanel.Children.Add(icon);
            }
        }
        catch { }

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock
        {
            Text = "MarkUp Markdown Editor",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = $"Version {version.Major}.{version.Minor}.{version.Build}",
            FontSize = 14,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray)
        });
        headerPanel.Children.Add(titleStack);
        aboutPanel.Children.Add(headerPanel);

        aboutPanel.Children.Add(new TextBlock
        {
            Text = "A modern, dark-mode Markdown editor and viewer for Windows.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14
        });

        var infoText = $"Build Date: {buildDate:yyyy-MM-dd}\n" +
                       $"Runtime: {runtimeVersion}\n" +
                       $"Architecture: {arch}\n" +
                       $"OS: {osVersion}\n" +
                       $"Windows App SDK: 1.8";

        aboutPanel.Children.Add(new TextBlock
        {
            Text = infoText,
            FontSize = 12,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray),
            IsTextSelectionEnabled = true
        });

        aboutPanel.Children.Add(new TextBlock
        {
            Text = "© 2025 John Donnelly. All rights reserved.",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray)
        });

        var dialog = new ContentDialog
        {
            Title = "About MarkUp",
            Content = aboutPanel,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    #endregion

    #region Splitter Drag

    private void Splitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isSplitterDragging = true;
        _splitterStartX = e.GetCurrentPoint(Content as UIElement).Position.X;
        _editorStartWidth = EditorColumn.ActualWidth;
        (sender as UIElement)?.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Splitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSplitterDragging) return;

        var currentX = e.GetCurrentPoint(Content as UIElement).Position.X;
        var delta = currentX - _splitterStartX;
        var totalWidth = EditorColumn.ActualWidth + PreviewColumn.ActualWidth;
        var minWidth = totalWidth * 0.20;
        var newEditorWidth = Math.Clamp(_editorStartWidth + delta, minWidth, totalWidth - minWidth);
        var newPreviewWidth = totalWidth - newEditorWidth;

        EditorColumn.Width = new GridLength(newEditorWidth, GridUnitType.Pixel);
        PreviewColumn.Width = new GridLength(newPreviewWidth, GridUnitType.Pixel);
        e.Handled = true;
    }

    private void Splitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isSplitterDragging = false;
        (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void Splitter_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.ChangeCursor(InputSystemCursorShape.SizeWestEast);
        }
    }

    private void Splitter_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.ChangeCursor(InputSystemCursorShape.Arrow);
        }
    }

    #endregion

    #region Dialogs

    private async Task<ContentDialogResult> ShowSavePromptAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Unsaved Changes",
            Content = "Do you want to save changes to this document?",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Don't Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync();
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    #endregion
}
