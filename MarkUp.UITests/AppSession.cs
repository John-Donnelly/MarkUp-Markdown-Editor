using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MarkUp.UITests;

public abstract class AppSession
{
    private const string WinAppDriverUrl      = "http://127.0.0.1:4723";
    private const string WinAppDriverPath     = @"C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe";
    private const string AppPackageName       = "JADApps.MarkUpMarkdownEditor";
    private const string AppWindowTitleSuffix = "— MarkUp";

    // ── Remote mode ──────────────────────────────────────────────────────────
    // Set UITEST_DRIVER_URL=http://192.168.0.100:4723 to run tests against MORPHEUS.
    // WinAppDriver must already be running on the remote machine before the test run starts.

    /// <summary>WinAppDriver endpoint on the remote test machine (MORPHEUS).</summary>
    private const string RemoteDriverUrl = "http://192.168.0.100:4723";

    /// <summary>
    /// Default path to the app executable AS SEEN BY WinAppDriver on the remote machine.
    /// Override with the <c>UITEST_REMOTE_APP_PATH</c> environment variable when the path differs.
    /// </summary>
    private const string RemoteAppPathDefault =
        @"C:\Users\John_\source\repos\MarkUp Markdown Editor\MarkUp Markdown Editor\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\MarkUp Markdown Editor.exe";

    /// <summary>Active WinAppDriver URL — local by default, remote when UITEST_DRIVER_URL is set.</summary>
    private static string DriverUrl =>
        Environment.GetEnvironmentVariable("UITEST_DRIVER_URL") ?? WinAppDriverUrl;

    /// <summary>True when <see cref="DriverUrl"/> points to a remote machine rather than localhost.</summary>
    private static bool IsRemoteMode =>
        !string.Equals(DriverUrl, WinAppDriverUrl, StringComparison.OrdinalIgnoreCase);

    // Retry timing for FindById (no IPC during the sleep — purely in-process)
    private const int ElementRetryMs  = 200;
    private const int ElementTimeoutS = 10;

    private static Process? _winAppDriverProcess;
    private static nint     _appWindowHandle;

    protected static WindowsDriver? Session       { get; private set; }
    protected static WindowsDriver? DesktopSession { get; private set; }
    protected static bool IsSessionAvailable => Session is not null;

    // ─────────────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    internal static void InitialiseSession()
    {
        if (IsRemoteMode)
        {
            InitialiseRemoteSession();
            return;
        }

        if (!EnsureWinAppDriverRunning())
        {
            Trace.WriteLine("[UITests] WinAppDriver is not available.");
            return;
        }

        var aumid = ResolveAumid();
        if (aumid is null) { Trace.WriteLine("[UITests] App not installed."); return; }

        try
        {
            foreach (var stale in Process.GetProcessesByName("MarkUp Markdown Editor"))
                try { stale.Kill(entireProcessTree: true); } catch { }
            Thread.Sleep(600);

            _ = ActivateApplication(aumid);
            nint hwnd = WaitForAppWindow(TimeSpan.FromSeconds(30));
            _appWindowHandle = hwnd;
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
            Thread.Sleep(2000); // Let the app fully render before the first test

            Session = CreateAppSession(hwnd);
            // 2000 ms implicit wait: long enough for FindFirst to traverse past the WebView2 node
            // (idle WebView2 UIA traversal takes ~200-1000 ms) to reach elements in Row 3+ of the
            // main grid (AutomationBridgePanel, FindReplace bar, StatusBar). 500 ms was too short.
            Session.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(2000);

            DesktopSession = CreateDesktopSession();
            DesktopSession.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);
        }
        catch (Exception ex) { Trace.WriteLine($"[UITests] Could not start session: {ex.Message}"); }
    }

    /// <summary>
    /// Initialises a session against WinAppDriver running on a remote machine.
    /// The remote WinAppDriver process must already be listening before this is called.
    /// The app is launched by WinAppDriver on the remote machine using the <c>app</c> capability;
    /// no local process management or Win32 HWND operations are performed.
    /// </summary>
    private static void InitialiseRemoteSession()
    {
        var appPath = Environment.GetEnvironmentVariable("UITEST_REMOTE_APP_PATH") ?? RemoteAppPathDefault;
        Trace.WriteLine($"[UITests] Remote mode — driver: {DriverUrl}  app: {appPath}");
        try
        {
            Session = CreateRemoteAppSession(appPath);
            Session.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(2000);
            DesktopSession = CreateRemoteDesktopSession();
            DesktopSession.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);
            WarmUpSessionRoot();
        }
        catch (Exception ex) { Trace.WriteLine($"[UITests] Could not start remote session: {ex.Message}"); }
    }

    internal static void CleanupSession()
    {
        try { Session?.Quit(); } catch { }
        Session = null;
        try { DesktopSession?.Quit(); } catch { }
        DesktopSession = null;
        try { if (_winAppDriverProcess is { HasExited: false }) _winAppDriverProcess.Kill(entireProcessTree: true); } catch { }
        _winAppDriverProcess = null;
        _appWindowHandle = 0;
    }

    /// <summary>
    /// Quits the current WinAppDriver sessions and reopens fresh ones to the same app window.
    /// Call this after any operation that permanently corrupts the UIA session
    /// (e.g. <see cref="CoreWebView2.ShowPrintUI"/>, which puts WebView2 into print mode and
    /// leaves the WinAppDriver UIA provider in an unrecoverable blocking state).
    /// </summary>
    protected static void ReinitializeSession()
    {
        try { Session?.Quit(); } catch { }
        Session = null;
        try { DesktopSession?.Quit(); } catch { }
        DesktopSession = null;

        if (IsRemoteMode)
        {
            InitialiseRemoteSession();
            return;
        }

        if (!EnsureWinAppDriverRunning()) return;

        try
        {
            _appWindowHandle = WaitForAppWindow(TimeSpan.FromSeconds(15));
            ShowWindow(_appWindowHandle, SW_RESTORE);
            SetForegroundWindow(_appWindowHandle);
            Thread.Sleep(500);
            Session = CreateAppSession(_appWindowHandle);
            Session.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(2000);
            DesktopSession = CreateDesktopSession();
            DesktopSession.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);
            WarmUpSessionRoot();
        }
        catch (Exception ex) { Trace.WriteLine($"[UITests] ReinitializeSession failed: {ex.Message}"); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Test helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Restores and focuses the app window. Pure Win32 — no WinAppDriver IPC.</summary>
    protected static void BringToFront()
    {
        if (_appWindowHandle == 0) return;
        ShowWindow(_appWindowHandle, SW_RESTORE);
        SetForegroundWindow(_appWindowHandle);
        Thread.Sleep(200);
    }

    protected static void SkipIfNoSession()
    {
        if (Session is null)
            ReinitializeSession();

        if (Session is null)
            Assert.Inconclusive("WinAppDriver session not available. See MarkUp.UITests\\README.md.");
    }

    /// <summary>
    /// Clears the editor to a known-empty state. Closes the find bar if open (via Escape — no UIA
    /// traversal needed), then Ctrl+A → Delete. Does NOT use File &gt; New.
    /// </summary>
    protected static void ResetToCleanState()
    {
        try { SendEscapeKey(); } catch { }  // Close find bar if open (harmless if not)
        Thread.Sleep(100);

        var editor = TryFindById("EditorTextBox");
        if (editor is null)
        {
            CleanupSession();
            InitialiseSession();
            SkipIfNoSession();
            editor = FindById("EditorTextBox");
        }

        editor.Click();
        Thread.Sleep(100);
        SendCtrlShortcut('A');
        Thread.Sleep(100);
        SendDeleteKey();
        Thread.Sleep(200);
    }

    /// <summary>Switches to split view (both panels visible). Used by <see cref="ViewWorkflowTests"/>.</summary>
    protected static void EnsureSplitView()
    {
        ClickMenu("MenuBarView", "MenuViewSplit");
        Thread.Sleep(300);
    }

    /// <summary>Resets zoom to 100% via menu. Used by <see cref="ViewWorkflowTests"/>.</summary>
    protected static void EnsureZoom100()
    {
        ClickMenu("MenuBarView", "MenuZoomReset");
        Thread.Sleep(150);
    }

    /// <summary>Shows the status bar if it was hidden. Used by <see cref="ViewWorkflowTests"/>.</summary>
    protected static void EnsureStatusBarVisible()
    {
        if (IsHidden("StatusBarStats"))
            ClickMenu("MenuBarView", "MenuToggleStatusBar");
    }

    /// <summary>
    /// Lightweight cleanup: brings the app to front and sends two Escape key presses.
    /// The first closes any open sub-menu or ContentDialog; the second closes any parent menu
    /// that may have been left open (e.g. File menu still open after export sub-menu test fails).
    /// Does NOT use <see cref="SendEscapeToFocused"/> — that could cancel a WebView2 back-navigation.
    /// </summary>
    protected static void DismissModal()
    {
        if (!IsSessionResponsive())
        {
            CleanupSession();
            InitialiseSession();
            return;
        }

        BringToFront();
        try { SendEscapeKey(); } catch { }  // Close submenu / ContentDialog
        Thread.Sleep(150);
        try { SendEscapeKey(); } catch { }  // Close parent menu (harmless if nothing open)
        Thread.Sleep(100);
    }

    /// <summary>Thorough transient-window dismissal. Use in specific test helpers, not in TestCleanup.</summary>
    protected static void DismissTransientWindows()
    {
        if (Session is null) return;
        try { TryFindById("CloseFindButton")?.Click(); } catch { }
        Thread.Sleep(100);
        try
        {
            foreach (var name in new[] { "Don't Save", "Cancel", "Close" })
            {
                var el = TryFindInAppByName(name);
                if (el is not null) { el.Click(); return; }
            }
        }
        catch { }
        try { TryFindDesktopByAnyName("Don't Save", "Cancel", "Close")?.Click(); } catch { }
        try { SendEscapeKey(); Thread.Sleep(150); } catch { }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Element finders
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds a required element by AutomationId, retrying for up to <see cref="ElementTimeoutS"/> seconds.
    /// Each attempt uses <see cref="TryFindById"/> which issues a <c>FindFirst</c> (not <c>FindAll</c>) call
    /// so traversal stops at the first match and never enters the WebView2 subtree for elements that precede
    /// it in the UIA tree. <see cref="ElementRetryMs"/> adds a short in-process back-off between retries.
    /// </summary>
    protected static AppiumElement FindById(string automationId)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < ElementTimeoutS)
        {
            var el = TryFindById(automationId) ?? TryFindByFallbackName(automationId);
            if (el is not null) return el;
            Thread.Sleep(ElementRetryMs);
        }
        var last = TryFindById(automationId) ?? TryFindByFallbackName(automationId);
        return last ?? throw new NoSuchElementException(
            $"Element '{automationId}' was not found within {ElementTimeoutS}s.");
    }

    /// <summary>
    /// Tries to find an element by AutomationId using <c>FindFirst</c> (not <c>FindAll</c>).
    /// <c>FindFirst</c> stops at the first match and does NOT traverse the WebView2 subtree
    /// for elements (Toolbar, MenuBar, EditorTextBox) that appear before it in the UIA tree,
    /// avoiding the WinAppDriver block that occurs when WebView2 is navigating.
    /// </summary>
    protected static AppiumElement? TryFindById(string automationId)
    {
        if (Session is null) return null;
        try { return Session.FindElement(MobileBy.AccessibilityId(automationId)); }
        catch (Exception ex) when (IsRecoverableWindowException(ex))
        {
            ReinitializeSession();
            if (Session is null) return null;
            try { return Session.FindElement(MobileBy.AccessibilityId(automationId)); }
            catch { return null; }
        }
        catch { return null; }
    }

    /// <summary>
    /// Finds a child element by AutomationId scoped to <paramref name="container"/>, retrying
    /// for up to <see cref="ElementTimeoutS"/> seconds. Limits UIA traversal to the container's
    /// subtree so Chrome's process-global hook only affects the small number of nodes inside
    /// the container rather than the entire window tree.
    /// </summary>
    protected static AppiumElement FindByIdWithin(AppiumElement container, string automationId)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < ElementTimeoutS)
        {
            var el = TryFindByIdWithin(container, automationId);
            if (el is not null) return el;
            Thread.Sleep(ElementRetryMs);
        }
        var last = TryFindByIdWithin(container, automationId);
        return last ?? throw new NoSuchElementException(
            $"Element '{automationId}' was not found within container in {ElementTimeoutS}s.");
    }

    /// <summary>Tries to find a child element by AutomationId scoped to the given container.</summary>
    protected static AppiumElement? TryFindByIdWithin(AppiumElement container, string automationId)
    {
        try { return container.FindElement(MobileBy.AccessibilityId(automationId)); }
        catch { return null; }
    }

    protected static AppiumElement GetCachedElement(ref AppiumElement? cache, string automationId)
    {
        if (cache is not null)
        {
            try
            {
                _ = cache.Enabled;
                return cache;
            }
            catch (Exception ex) when (IsRecoverableElementException(ex))
            {
                CleanupSession();
                InitialiseSession();
                cache = null;
            }
        }

        cache = FindById(automationId);
        return cache;
    }

    protected static AppiumElement GetCachedElementWithin(ref AppiumElement? cache, ref AppiumElement? containerCache,
        string containerAutomationId, string automationId)
    {
        AppiumElement container;
        try
        {
            container = GetCachedElement(ref containerCache, containerAutomationId);
        }
        catch (NoSuchElementException)
        {
            cache = FindById(automationId);
            return cache;
        }

        if (cache is not null)
        {
            try
            {
                _ = cache.Enabled;
                return cache;
            }
            catch (Exception ex) when (IsRecoverableElementException(ex))
            {
                CleanupSession();
                InitialiseSession();
                containerCache = null;
                container = GetCachedElement(ref containerCache, containerAutomationId);
                cache = null;
            }
        }

        cache = FindByIdWithin(container, automationId);
        return cache;
    }

    private static bool IsRecoverableWindowException(Exception ex) =>
        ex is NoSuchWindowException
        || ex is NotImplementedException { Message: var notImplementedMessage }
            && notImplementedMessage.Contains("requested resource could not be found", StringComparison.OrdinalIgnoreCase)
        || ex is WebDriverException { Message: var webDriverMessage }
            && (webDriverMessage.Contains("window has been closed", StringComparison.OrdinalIgnoreCase)
                || webDriverMessage.Contains("requested resource could not be found", StringComparison.OrdinalIgnoreCase));

    private static bool IsRecoverableElementException(Exception ex) =>
        IsRecoverableWindowException(ex) || ex is StaleElementReferenceException;

    private static bool IsSessionResponsive()
    {
        if (Session is null) return false;

        try
        {
            _ = Session.Title;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void WarmUpSessionRoot()
    {
        if (Session is null) return;

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(15))
        {
            try
            {
                var editor = Session.FindElement(MobileBy.AccessibilityId("EditorTextBox"));
                if (editor is not null)
                    return;
            }
            catch { }

            Thread.Sleep(250);
        }
    }

    protected static bool IsDisplayed(string automationId) =>
        TryFindById(automationId)?.Displayed == true;

    protected static bool IsHidden(string automationId) =>
        !IsDisplayed(automationId);

    protected static void ClickMenu(string menuBarId, string menuItemId)
    {
        FindById(menuBarId).Click();
        Thread.Sleep(350);
        FindById(menuItemId).Click();
        Thread.Sleep(200);
    }

    protected static void ClickSubMenu(string menuBarId, string subMenuId, string menuItemId)
    {
        FindById(menuBarId).Click();
        Thread.Sleep(350);
        FindById(subMenuId).Click();
        Thread.Sleep(300);

        // Try the app session first (AccessibilityId then Name fallback)
        var item = TryFindById(menuItemId);

        // WinUI3 flyout sub-menus open in the desktop UIA tree, not the app session tree.
        // Fall back to DesktopSession when the item is not visible in the app session.
        if (item is null && DesktopSession is not null)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < ElementTimeoutS)
            {
                try
                {
                    item = DesktopSession.FindElement(MobileBy.AccessibilityId(menuItemId));
                    if (item is not null) break;
                }
                catch { }

                var name = GetFallbackName(menuItemId);
                if (name is not null)
                {
                    try
                    {
                        item = DesktopSession.FindElement(MobileBy.Name(name));
                        if (item is not null) break;
                    }
                    catch { }
                }

                Thread.Sleep(ElementRetryMs);
            }
        }

        if (item is null)
            throw new NoSuchElementException(
                $"Element '{menuItemId}' was not found within {ElementTimeoutS}s.");

        item.Click();
        Thread.Sleep(200);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Desktop / app name searches
    // ─────────────────────────────────────────────────────────────────────────

    protected static AppiumElement? TryFindDesktopByName(string name)
    {
        if (DesktopSession is null) return null;
        try
        {
            var els = DesktopSession.FindElements(MobileBy.Name(name));
            return els.Count > 0 ? els[0] : null;
        }
        catch { return null; }
    }

    protected static AppiumElement? TryFindDesktopByAnyName(params string[] names)
    {
        foreach (var n in names)
        {
            var el = TryFindDesktopByName(n);
            if (el is not null) return el;
        }
        return null;
    }

    protected static AppiumElement? WaitForDesktopByAnyName(TimeSpan timeout, params string[] names)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var el = TryFindDesktopByAnyName(names);
            if (el is not null) return el;
            Thread.Sleep(100);
        }
        return TryFindDesktopByAnyName(names);
    }

    protected static AppiumElement? TryFindInAppByName(string name)
    {
        if (Session is null) return null;
        try { return Session.FindElement(MobileBy.Name(name)); }
        catch { return null; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Keyboard injection (Win32 keybd_event — no WinAppDriver IPC)
    // ─────────────────────────────────────────────────────────────────────────

    protected static void SendCtrlShortcut(char key)       => SendModifiedShortcut(VK_CONTROL, ToVirtualKey(key));
    protected static void SendCtrlShortcut(ushort vk)      => SendModifiedShortcut(VK_CONTROL, vk);
    protected static void SendCtrlShiftShortcut(char key)  => SendModifiedShortcut(VK_CONTROL, VK_SHIFT, ToVirtualKey(key));
    protected static void SendCtrlShiftShortcut(ushort vk) => SendModifiedShortcut(VK_CONTROL, VK_SHIFT, vk);
    protected static void SendAltShortcut(ushort vk)       => SendModifiedShortcut(VK_MENU, vk);
    /// <summary>
    /// Sets the system clipboard to <paramref name="text"/> (via Win32 API) and then sends
    /// Ctrl+V to the focused control. This is layout-independent and triggers WinUI3 TextBox
    /// <c>TextChanged</c> reliably — unlike <see cref="keybd_event"/> character injection.
    /// </summary>
    protected static void PasteText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        SetClipboard(text);
        Thread.Sleep(30);
        // Paste into whichever control currently has keyboard focus
        EnsureAppFocused();
        SendModifiedShortcut(VK_CONTROL, (ushort)'V');
    }

    /// <summary>
    /// Sets the system clipboard to <paramref name="text"/> via Win32 CF_UNICODETEXT.
    /// Use before calling <see cref="ClickMenu"/> to paste via the Edit > Paste menu item,
    /// which reliably triggers <c>UpdateStatusBar</c> regardless of which panel has focus.
    /// </summary>
    protected static void SetClipboard(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        var bytes = System.Text.Encoding.Unicode.GetBytes(text + '\0');
        var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (nuint)bytes.Length);
        var ptr = GlobalLock(hGlobal);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        GlobalUnlock(hGlobal);
        OpenClipboard(IntPtr.Zero);
        EmptyClipboard();
        SetClipboardData(CF_UNICODETEXT, hGlobal);
        CloseClipboard();
    }

    protected static void SendHashCharacter()              => PasteText("#");
    protected static void SendText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureAppFocused();
        foreach (var ch in text)
            SendCharacter(ch);
        Thread.Sleep(150);
    }

    protected static void SendCtrlAddShortcut()            => SendModifiedShortcut(VK_CONTROL, VK_ADD);
    protected static void SendCtrlSubtractShortcut()       => SendModifiedShortcut(VK_CONTROL, VK_SUBTRACT);
    protected static void SendEscapeKey()                  => SendVirtualKey(VK_ESCAPE);
    /// <summary>
    /// Sends Escape to whatever window currently has keyboard focus — no <see cref="SetForegroundWindow"/> call.
    /// Use to close native file dialogs or WebView2 browser dialogs that already own focus;
    /// calling <see cref="SendEscapeKey"/> first would steal focus away from the dialog to the app window.
    /// </summary>
    protected static void SendEscapeToFocused()
    {
        keybd_event((byte)VK_ESCAPE, 0, 0, 0);
        Thread.Sleep(50);
        keybd_event((byte)VK_ESCAPE, 0, KEYEVENTF_KEYUP, 0);
        Thread.Sleep(300);
    }
    protected static void SendDeleteKey()                  => SendVirtualKey(VK_DELETE);
    protected static void SendEnterKey()                   => SendVirtualKey(VK_RETURN);
    protected static void SendDownKey()                    => SendVirtualKey(VK_DOWN);
    protected static void SendRightKey()                   => SendVirtualKey(VK_RIGHT);

    private static void SendModifiedShortcut(params ushort[] virtualKeys)
    {
        EnsureAppFocused();
        foreach (var key in virtualKeys[..^1])
            keybd_event((byte)key, 0, 0, 0);
        keybd_event((byte)virtualKeys[^1], 0, 0, 0);
        Thread.Sleep(50);
        keybd_event((byte)virtualKeys[^1], 0, KEYEVENTF_KEYUP, 0);
        for (int i = virtualKeys.Length - 2; i >= 0; i--)
            keybd_event((byte)virtualKeys[i], 0, KEYEVENTF_KEYUP, 0);
        Thread.Sleep(200);
    }

    private static void SendVirtualKey(ushort vk)
    {
        EnsureAppFocused();
        keybd_event((byte)vk, 0, 0, 0);
        Thread.Sleep(50);
        keybd_event((byte)vk, 0, KEYEVENTF_KEYUP, 0);
        Thread.Sleep(150);
    }

    private static void EnsureAppFocused()
    {
        // In remote mode _appWindowHandle is always 0 — Win32 keybd_event sends keystrokes to
        // the local machine and has no effect on the remote session; skip the focus call silently.
        if (_appWindowHandle == 0) return;
        SetForegroundWindow(_appWindowHandle);
        Thread.Sleep(80);
    }

    private static ushort ToVirtualKey(char key)
    {
        var upper = char.ToUpperInvariant(key);
        if ((upper >= 'A' && upper <= 'Z') || (upper >= '0' && upper <= '9'))
            return upper;
        throw new ArgumentOutOfRangeException(nameof(key), key, "Only A-Z and 0-9 shortcut keys are supported.");
    }

    private static void SendCharacter(char ch)
    {
        short key = VkKeyScan(ch);
        if (key == -1)
            throw new ArgumentOutOfRangeException(nameof(ch), ch, "Character cannot be translated to a virtual key.");

        byte vk = (byte)(key & 0xFF);
        byte shiftState = (byte)((key >> 8) & 0xFF);

        if ((shiftState & 1) != 0) keybd_event((byte)VK_SHIFT, 0, 0, 0);
        if ((shiftState & 2) != 0) keybd_event((byte)VK_CONTROL, 0, 0, 0);
        if ((shiftState & 4) != 0) keybd_event((byte)VK_MENU, 0, 0, 0);

        keybd_event(vk, 0, 0, 0);
        Thread.Sleep(30);
        keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);

        if ((shiftState & 4) != 0) keybd_event((byte)VK_MENU, 0, KEYEVENTF_KEYUP, 0);
        if ((shiftState & 2) != 0) keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
        if ((shiftState & 1) != 0) keybd_event((byte)VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);

        Thread.Sleep(40);
    }

    private static void SendAltNumpadCode(string digits)
    {
        EnsureAppFocused();
        keybd_event((byte)VK_MENU, 0, 0, 0);
        foreach (var digit in digits)
        {
            byte numpadKey = digit switch
            {
                '0' => 0x60,
                '1' => 0x61,
                '2' => 0x62,
                '3' => 0x63,
                '4' => 0x64,
                '5' => 0x65,
                '6' => 0x66,
                '7' => 0x67,
                '8' => 0x68,
                '9' => 0x69,
                _ => throw new ArgumentOutOfRangeException(nameof(digits), digits, "Alt+numpad digits must be 0-9.")
            };

            keybd_event(numpadKey, 0, 0, 0);
            Thread.Sleep(30);
            keybd_event(numpadKey, 0, KEYEVENTF_KEYUP, 0);
            Thread.Sleep(30);
        }

        keybd_event((byte)VK_MENU, 0, KEYEVENTF_KEYUP, 0);
        Thread.Sleep(100);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  WinAppDriver startup
    // ─────────────────────────────────────────────────────────────────────────

    private static bool EnsureWinAppDriverRunning()
    {
        if (IsWinAppDriverListening()) return true;

        if (!File.Exists(WinAppDriverPath))
        {
            Trace.WriteLine($"[UITests] WinAppDriver not found at '{WinAppDriverPath}'.");
            return false;
        }

        if (TryStartWinAppDriver(elevated: false, TimeSpan.FromSeconds(5)))
            return true;

        try { if (_winAppDriverProcess is { HasExited: false }) _winAppDriverProcess.Kill(entireProcessTree: true); } catch { }
        _winAppDriverProcess = null;

        return TryStartWinAppDriver(elevated: true, TimeSpan.FromSeconds(20));
    }

    private static bool TryStartWinAppDriver(bool elevated, TimeSpan timeout)
    {
        try
        {
            _winAppDriverProcess = Process.Start(new ProcessStartInfo
            {
                FileName       = WinAppDriverPath,
                UseShellExecute = elevated,
                Verb           = elevated ? "runas" : string.Empty
            });
        }
        catch (Win32Exception ex)
        {
            Trace.WriteLine($"[UITests] Could not start WinAppDriver{(elevated ? " (elevated)" : string.Empty)}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[UITests] Could not start WinAppDriver: {ex.Message}");
            return false;
        }

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (IsWinAppDriverListening()) return true;
            if (_winAppDriverProcess is { HasExited: true }) return false;
            Thread.Sleep(250);
        }
        return IsWinAppDriverListening();
    }

    private static bool IsWinAppDriverListening()
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync("127.0.0.1", 4723).Wait(1000) && client.Connected;
        }
        catch { return false; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  App activation & window discovery
    // ─────────────────────────────────────────────────────────────────────────

    private static string? ResolveAumid()
    {
        try
        {
            using var ps = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "powershell.exe",
                    Arguments              = $"-NoProfile -Command \"(Get-AppxPackage -Name '{AppPackageName}*').PackageFamilyName\"",
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                }
            };
            ps.Start();
            string pfn = ps.StandardOutput.ReadToEnd().Trim();
            ps.WaitForExit();
            return string.IsNullOrWhiteSpace(pfn) ? null : $"{pfn}!App";
        }
        catch { return null; }
    }

    private static nint WaitForAppWindow(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var hwnd = FindAppWindowHandle();
            if (hwnd != 0) return hwnd;
            Thread.Sleep(500);
        }
        throw new TimeoutException(
            $"App window with title ending '{AppWindowTitleSuffix}' did not appear within {timeout.TotalSeconds}s.");
    }

    private static WindowsDriver CreateAppSession(nint hwnd)
    {
        var options = new AppiumOptions();
        options.AutomationName = "Windows";
        options.PlatformName   = "Windows";
        options.AddAdditionalAppiumOption("appTopLevelWindow", $"0x{hwnd:X}");
        return new WindowsDriver(new Uri(WinAppDriverUrl), options, TimeSpan.FromSeconds(120));
    }

    private static WindowsDriver CreateDesktopSession()
    {
        var options = new AppiumOptions();
        options.AutomationName = "Windows";
        options.PlatformName   = "Windows";
        options.App            = "Root";
        return new WindowsDriver(new Uri(WinAppDriverUrl), options, TimeSpan.FromSeconds(120));
    }

    /// <summary>
    /// Creates a WinAppDriver session on the remote machine, launching the app from <paramref name="appPath"/>
    /// (the path must be valid on the remote machine, not on this machine).
    /// </summary>
    private static WindowsDriver CreateRemoteAppSession(string appPath)
    {
        var options = new AppiumOptions();
        options.AutomationName = "Windows";
        options.PlatformName   = "Windows";
        options.DeviceName     = "WindowsPC";
        options.App            = appPath;
        return new WindowsDriver(new Uri(DriverUrl), options, TimeSpan.FromSeconds(120));
    }

    /// <summary>Creates a desktop-root session connected to the remote WinAppDriver endpoint.</summary>
    private static WindowsDriver CreateRemoteDesktopSession()
    {
        var options = new AppiumOptions();
        options.AutomationName = "Windows";
        options.PlatformName   = "Windows";
        options.App            = "Root";
        return new WindowsDriver(new Uri(DriverUrl), options, TimeSpan.FromSeconds(120));
    }

    private static AppiumElement? TryFindByFallbackName(string automationId)
    {
        var name = GetFallbackName(automationId);
        if (string.IsNullOrWhiteSpace(name) || Session is null) return null;
        try { return Session.FindElement(MobileBy.Name(name)); }
        catch { return null; }
    }

    private static string? GetFallbackName(string automationId) => automationId switch
    {
        "MenuBarFile"          => "File",
        "MenuBarEdit"          => "Edit",
        "MenuBarFormat"        => "Format",
        "MenuBarView"          => "View",
        "MenuBarHelp"          => "Help",
        "MenuNew"              => "New",
        "MenuOpen"             => "Open...",
        "MenuSave"             => "Save",
        "MenuSaveAs"           => "Save As...",
        "MenuExport"           => "Export",
        "MenuExportHtml"       => "Export as HTML...",
        "MenuExportPlainText"  => "Export as Plain Text...",
        "MenuExportPdf"        => "Export as PDF...",
        "MenuPrint"            => "Print...",
        "MenuExit"             => "Exit",
        "MenuUndo"             => "Undo",
        "MenuRedo"             => "Redo",
        "MenuCut"              => "Cut",
        "MenuCopy"             => "Copy",
        "MenuPaste"            => "Paste",
        "MenuSelectAll"        => "Select All",
        "MenuFind"             => "Find & Replace...",
        "MenuBold"             => "Bold",
        "MenuItalic"           => "Italic",
        "MenuStrikethrough"    => "Strikethrough",
        "MenuInlineCode"       => "Inline Code",
        "MenuHeading"          => "Heading",
        "MenuHeading1"         => "Heading 1",
        "MenuHeading2"         => "Heading 2",
        "MenuHeading3"         => "Heading 3",
        "MenuHeading4"         => "Heading 4",
        "MenuHeading5"         => "Heading 5",
        "MenuHeading6"         => "Heading 6",
        "MenuHorizontalRule"   => "Horizontal Rule",
        "MenuUnorderedList"    => "Bullet List",
        "MenuOrderedList"      => "Numbered List",
        "MenuTaskList"         => "Task List",
        "MenuBlockquote"       => "Blockquote",
        "MenuCodeBlock"        => "Code Block",
        "MenuInsertLink"       => "Insert Link",
        "MenuInsertImage"      => "Insert Image",
        "MenuInsertTable"      => "Insert Table",
        "MenuViewEditor"       => "Editor Only",
        "MenuViewPreview"      => "Preview Only",
        "MenuViewSplit"        => "Split View",
        "MenuToggleWordWrap"   => "Toggle Word Wrap",
        "MenuZoomIn"           => "Zoom In",
        "MenuZoomOut"          => "Zoom Out",
        "MenuZoomReset"        => "Reset Zoom",
        "MenuToggleStatusBar"  => "Toggle Status Bar",
        "MenuMarkdownRef"      => "Markdown Reference",
        "MenuAbout"            => "About MarkUp",
        _                      => null
    };

    // ─────────────────────────────────────────────────────────────────────────
    //  COM activation
    // ─────────────────────────────────────────────────────────────────────────

    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments, uint options, out uint processId);
        int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            nint pSFI, [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint processId);
        int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            nint eventArgs, out uint processId);
    }

    [ComImport, Guid("45ba127d-10a8-46ea-8ab7-56ea9078943c"), ClassInterface(ClassInterfaceType.None)]
    private class ApplicationActivationManagerClass { }

    private static int ActivateApplication(string aumid)
    {
        var mgr = (IApplicationActivationManager)Activator.CreateInstance(typeof(ApplicationActivationManagerClass))!;
        int hr = mgr.ActivateApplication(aumid, null, 0, out uint pid);
        if (hr < 0) throw new InvalidOperationException($"ActivateApplication failed: 0x{hr:X8}");
        return (int)pid;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Win32
    // ─────────────────────────────────────────────────────────────────────────

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    private const uint   KEYEVENTF_KEYUP  = 0x0002;
    private const uint   CF_UNICODETEXT   = 13;
    private const uint   GMEM_MOVEABLE    = 0x0002;
    private const int    SW_RESTORE      = 9;
    private const ushort VK_CONTROL      = 0x11;
    private const ushort VK_SHIFT        = 0x10;
    private const ushort VK_MENU         = 0x12;
    private const ushort VK_ESCAPE       = 0x1B;
    private const ushort VK_RETURN       = 0x0D;
    private const ushort VK_DELETE       = 0x2E;
    private const ushort VK_DOWN         = 0x28;
    private const ushort VK_RIGHT        = 0x27;
    private const ushort VK_ADD          = 0x6B;
    private const ushort VK_SUBTRACT     = 0x6D;

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc fn, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(nint hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern short VkKeyScan(char ch);
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint hMem);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint hMem);
    [DllImport("user32.dll")] private static extern bool OpenClipboard(nint hWndNewOwner);
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern nint SetClipboardData(uint uFormat, nint hMem);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();

    private static nint FindAppWindowHandle()
    {
        foreach (var process in Process.GetProcessesByName("MarkUp Markdown Editor"))
        {
            try
            {
                process.Refresh();
                if (process.MainWindowHandle != 0)
                    return process.MainWindowHandle;
            }
            catch { }
        }

        return FindTopLevelWindowByTitleSuffix(AppWindowTitleSuffix);
    }

    private static nint FindTopLevelWindowByTitleSuffix(string suffix)
    {
        nint found = 0;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            var title = GetWindowTitle(hWnd);
            if (title.EndsWith(suffix, StringComparison.Ordinal)) { found = hWnd; return false; }
            return true;
        }, 0);
        return found;
    }

    private static string GetWindowTitle(nint hWnd)
    {
        int len = GetWindowTextLength(hWnd);
        if (len <= 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
